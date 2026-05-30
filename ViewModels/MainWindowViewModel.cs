using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Core.Abstractions;
using Lumen.Core.Models;
using Lumen.Services.Catalog;
using Lumen.Services.Library;
using Lumen.Services.Scanning;
using Lumen.Services.Settings;

namespace Lumen.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ILibraryScanner _scanner;
    private readonly InMemoryLibraryIndex _index;
    private readonly IAppSettingsStore _store;
    private TopLevel? _topLevel;
    private readonly SemaphoreSlim _thumbGate = new(6, 6);

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Preparing library…";
    [ObservableProperty] private FolderTreeNodeViewModel? _selectedFolder;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private double _thumbnailSize = 168;
    [ObservableProperty] private int _totalPhotoCount;

    private bool _applyingScanResults;

    public EditSessionViewModel EditSession { get; } = new();
    public ObservableCollection<FolderTreeNodeViewModel> RootFolders { get; } = new();
    public ObservableCollection<FolderGroupViewModel> FolderGroups { get; } = new();

    public MainWindowViewModel(ILibraryScanner scanner, InMemoryLibraryIndex index, IAppSettingsStore store)
    {
        _scanner = scanner;
        _index = index;
        _store = store;
    }

    public void AttachTopLevel(TopLevel topLevel) => _topLevel = topLevel;

    private bool _initialized;

    public async Task InitializeAsync(Window owner)
    {
        if (_initialized)
            return;

        _initialized = true;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            EnsureMacStartupLibrary();
        else
        {
            var settings = _store.Load();
            if (!settings.FirstRunCompleted)
                await new FirstRunWindow(_store, LibrarySetupMode.FirstRun).ShowDialog(owner).ConfigureAwait(true);
        }

        var loaded = _store.Load();
        if (loaded.ScanRoots.Count == 0)
            ApplyFallbackRootsFromCatalog();

        PruneOverlappingMacScanRoots(loaded);
        await ScanLibraryAsync().ConfigureAwait(true);
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (_applyingScanResults)
            return;
        _ = RefreshCenterAsync();
    }

    partial void OnSelectedFolderChanged(FolderTreeNodeViewModel? value)
    {
        if (_applyingScanResults || value is null)
            return;

        StatusText = $"Timeline · {value.Label}";
        ScrollToFolderRequested?.Invoke(value.AbsolutePath);
    }

    public event Action<string>? ScrollToFolderRequested;

    [RelayCommand]
    private async Task RescanLibraryAsync() => await ScanLibraryAsync().ConfigureAwait(true);

    [RelayCommand]
    private async Task OpenLibrarySetupAsync()
    {
        if (_topLevel is not Window w)
        {
            StatusText = "Window is not ready.";
            return;
        }

        await new FirstRunWindow(_store, LibrarySetupMode.Edit).ShowDialog(w).ConfigureAwait(true);
        await ScanLibraryAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddAccessibleFolderAsync()
    {
        if (_topLevel is not Window w)
        {
            StatusText = "Window is not ready.";
            return;
        }

        var folders = await w.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Grant access to a folder",
            AllowMultiple = false
        }).ConfigureAwait(true);

        if (folders.Count == 0)
            return;

        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        path = Path.TrimEndingDirectorySeparator(path);
        var s = _store.Load();
        if (!s.ScanRoots.Any(r => string.Equals(Path.TrimEndingDirectorySeparator(r), path, StringComparison.OrdinalIgnoreCase)))
            s.ScanRoots.Add(path);

        s.FirstRunCompleted = true;
        _store.Save(s);
        await ScanLibraryAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenEditAsync(PhotoTileViewModel? tile)
    {
        if (tile is null)
            return;

        IsEditMode = true;
        EditSession.SetFilmstrip(GetTimelineTiles(), tile);
        await EditSession.LoadPhotoAsync(tile.AbsolutePath, tile.Caption).ConfigureAwait(true);
        StatusText = tile.Caption;
    }

    [RelayCommand]
    private void CloseEdit()
    {
        IsEditMode = false;
    }

    public void NavigateEditPhoto(int delta)
    {
        if (!IsEditMode || EditSession.Filmstrip.Count == 0)
            return;

        var current = EditSession.GetFilmstripIndex() ?? 0;
        var next = current + delta;
        if (!EditSession.TrySelectFilmstripIndex(next))
            return;

        var tile = EditSession.Filmstrip[next];
        StatusText = $"{next + 1} / {EditSession.Filmstrip.Count} — {tile.Caption}";
    }

    public void SelectFilmstripTile(PhotoTileViewModel tile)
    {
        foreach (var item in EditSession.Filmstrip)
            item.IsSelected = ReferenceEquals(item, tile);

        EditSession.SelectedFilmstripItem = tile;
    }

    [RelayCommand]
    private async Task ExportPhotoAsync()
    {
        if (_topLevel is not Window w || string.IsNullOrEmpty(EditSession.FilePath))
            return;

        var file = await w.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export photo",
            SuggestedFileName = Path.GetFileNameWithoutExtension(EditSession.FilePath) + "_edited.png",
            FileTypeChoices =
            [
                new FilePickerFileType("PNG") { Patterns = ["*.png"] },
                new FilePickerFileType("JPEG") { Patterns = ["*.jpg", "*.jpeg"] }
            ]
        }).ConfigureAwait(true);

        if (file is null)
            return;

        var path = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        await EditSession.ExportAsync(path).ConfigureAwait(true);
        StatusText = $"Exported to {path}";
    }

    private void PruneOverlappingMacScanRoots(AppSettings settings)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrWhiteSpace(pictures))
            return;

        pictures = Path.TrimEndingDirectorySeparator(pictures);
        var home = Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        var normalized = settings.ScanRoots
            .Select(Path.TrimEndingDirectorySeparator)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Any(p => string.Equals(p, pictures, StringComparison.OrdinalIgnoreCase)))
            normalized = normalized.Where(p => !string.Equals(p, home, StringComparison.OrdinalIgnoreCase)).ToList();

        if (normalized.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(settings.ScanRoots.Select(Path.TrimEndingDirectorySeparator)))
            return;

        settings.ScanRoots = normalized;
        settings.FirstRunCompleted = true;
        _store.Save(settings);
    }

    private void ApplyFallbackRootsFromCatalog()
    {
        var roots = LibraryLocationCatalog.GetSuggestedScanCandidates()
            .Select(c => Path.TrimEndingDirectorySeparator(c.AbsolutePath))
            .ToList();

        var s = _store.Load();
        s.FirstRunCompleted = true;
        s.ScanRoots = roots;
        _store.Save(s);
    }

    private void EnsureMacStartupLibrary()
    {
        var autoRoots = LibraryLocationCatalog.GetMacOsAutoLoadRoots()
            .Select(c => Path.TrimEndingDirectorySeparator(c.AbsolutePath))
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (autoRoots.Count == 0)
            return;

        var s = _store.Load();
        var merged = s.ScanRoots
            .Select(Path.TrimEndingDirectorySeparator)
            .Concat(autoRoots)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (merged.Count == 0)
            merged = autoRoots;

        s.FirstRunCompleted = true;
        s.ScanRoots = merged;
        _store.Save(s);
    }

    private async Task ScanLibraryAsync()
    {
        CloseEdit();

        var roots = _store.Load().ScanRoots
            .Select(Path.TrimEndingDirectorySeparator)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roots.Count == 0)
        {
            StatusText = "No valid library locations.";
            ClearCenter();
            RootFolders.Clear();
            TotalPhotoCount = 0;
            return;
        }

        IsBusy = true;
        StatusText = "Scanning…";
        ClearCenter();

        try
        {
            _index.ScanRoots = roots;
            var progress = new Progress<int>(n => StatusText = $"Scanning… {n} files");

            await _index.RebuildAsync(
                _scanner.ScanAsync(roots, progress),
                CancellationToken.None).ConfigureAwait(true);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(RebuildFolderTree);

            _applyingScanResults = true;
            try
            {
                SelectedFolder = RootFolders.FirstOrDefault();
                await RefreshCenterAsync().ConfigureAwait(true);
            }
            finally
            {
                _applyingScanResults = false;
            }

            TotalPhotoCount = _index.TotalPhotoCount();
            StatusText = $"Indexed {TotalPhotoCount} photos.";
        }
        catch (Exception ex) when (FileSystemPhotoScanner.IsBenignAccessFailure(ex))
        {
            TotalPhotoCount = _index.TotalPhotoCount();
            StatusText = $"Indexed {TotalPhotoCount} photos (some folders skipped).";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildFolderTree()
    {
        RootFolders.Clear();
        foreach (var node in _index.GetFolderTree())
            RootFolders.Add(new FolderTreeNodeViewModel(node));
    }

    public IReadOnlyList<PhotoTileViewModel> GetTimelineTiles() =>
        FolderGroups.SelectMany(g => g.Tiles).ToList();

    private void ClearCenter()
    {
        foreach (var g in FolderGroups)
            g.Dispose();
        FolderGroups.Clear();
    }

    private async Task RefreshCenterAsync()
    {
        ClearCenter();

        var buckets = _index.GetPhotosGroupedByFolder(folderPrefix: null);
        var query = SearchQuery.Trim();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var bucket in buckets)
        {
            var group = new FolderGroupViewModel(FormatFolderTitle(bucket.FolderPath), bucket.FolderPath);

            foreach (var photo in bucket.Photos)
            {
                if (!seenPaths.Add(photo.AbsolutePath))
                    continue;

                var name = Path.GetFileName(photo.AbsolutePath);
                if (!string.IsNullOrEmpty(query) &&
                    name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                group.Tiles.Add(new PhotoTileViewModel(photo.AbsolutePath, name));
            }

            if (group.Tiles.Count == 0)
                continue;

            FolderGroups.Add(group);
        }

        await LoadVisibleThumbnailsAsync().ConfigureAwait(true);
    }

    private string FormatFolderTitle(string directoryPath)
    {
        directoryPath = Path.TrimEndingDirectorySeparator(directoryPath);
        if (string.IsNullOrEmpty(directoryPath))
            return "Unknown folder";

        foreach (var root in _index.ScanRoots
                     .Select(Path.TrimEndingDirectorySeparator)
                     .Where(r => !string.IsNullOrWhiteSpace(r))
                     .OrderByDescending(r => r.Length))
        {
            if (!directoryPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                continue;

            var rel = Path.GetRelativePath(root, directoryPath);
            if (string.IsNullOrEmpty(rel) || rel == ".")
                return Path.GetFileName(root) ?? root;

            return rel.Replace(Path.DirectorySeparatorChar, '/');
        }

        return directoryPath;
    }

    private async Task LoadVisibleThumbnailsAsync()
    {
        var tiles = GetTimelineTiles();
        var token = CancellationToken.None;

        await Task.WhenAll(tiles.Select(async tile =>
        {
            await _thumbGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await tile.LoadThumbnailAsync(token).ConfigureAwait(false);
            }
            finally
            {
                _thumbGate.Release();
            }
        })).ConfigureAwait(true);
    }
}
