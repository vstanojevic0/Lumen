using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Core.Abstractions;
using Lumen.Core.Models;
using Lumen.Services.Catalog;
using Lumen.Services.Imaging;
using Lumen.Services.Library;
using Lumen.Services.Scanning;
using Lumen.Services.Settings;
using Lumen.Services.Web;

namespace Lumen.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly ILibraryScanner _scanner;
    private readonly InMemoryLibraryIndex _index;
    private readonly IAppSettingsStore _store;
    private TopLevel? _topLevel;
    private CancellationTokenSource? _refreshCts;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Preparing library…";
    [ObservableProperty] private LibraryViewMode _viewMode = LibraryViewMode.Library;
    [ObservableProperty] private string? _openFolderPath;
    [ObservableProperty] private string _centerTitle = "Library";
    [ObservableProperty] private FolderListItemViewModel? _selectedFolder;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private int _totalPhotoCount;
    [ObservableProperty] private int _previewZoomPercent = 100;
    [ObservableProperty] private string _workspaceMode = "Library";

    public bool IsEditWorkspace => IsEditMode;
    public bool IsLibraryGridVisible => !IsEditMode && IsPhotoTimelineVisible;
    public bool IsFolderAlbumGridVisible => !IsEditMode && IsFolderAlbumsMode;

    private bool _applyingScanResults;

    public ThumbnailLoadQueue ThumbnailQueue { get; } = new();
    public EditSessionViewModel EditSession { get; } = new();
    public ObservableCollection<FolderListItemViewModel> FolderList { get; } = new();
    public ObservableCollection<MonthSectionViewModel> GallerySections { get; } = new();
    public ObservableCollection<FolderAlbumViewModel> FolderAlbums { get; } = new();

    public bool IsLibraryMode => ViewMode == LibraryViewMode.Library;
    public bool IsFolderAlbumsMode => ViewMode == LibraryViewMode.FolderAlbums;
    public bool IsFolderPhotosMode => ViewMode == LibraryViewMode.FolderPhotos;
    public bool IsPhotoTimelineVisible => IsLibraryMode || IsFolderPhotosMode;
    public bool IsCenterTimelineVisible => IsPhotoTimelineVisible;
    public bool IsCenterFolderAlbumsVisible => IsFolderAlbumsMode;
    public bool ShowSidebarFolderList => IsFolderAlbumsMode || IsFolderPhotosMode;

    private PhotoTileViewModel? _inspectorTile;

    public event Action? LibraryUpdated;

    public MainWindowViewModel(ILibraryScanner scanner, InMemoryLibraryIndex index, IAppSettingsStore store)
    {
        _scanner = scanner;
        _index = index;
        _store = store;
    }

    public void AttachTopLevel(TopLevel topLevel) => _topLevel = topLevel;

    public WebStatusDto GetWebStatus() =>
        new(
            TotalPhotoCount,
            StatusText,
            IsBusy,
            GetFavoritePathSet().Count,
            WebUiSource.MediaBaseUri?.ToString());

    public WebGallerySnapshot GetWebGallerySnapshot(string? folderPath = null, bool favoritesOnly = false)
    {
        var favorites = GetFavoritePathSet();
        var sections = BuildGallerySections(folderPath, favoritesOnly, favorites)
            .Select(s => new WebGallerySectionDto(
                FormatWebSectionTitle(s.HeaderText),
                s.Photos.Select(p => new WebPhotoDto(
                    p.AbsolutePath,
                    p.Caption,
                    favorites.Contains(p.AbsolutePath))).ToList()))
            .ToList();

        var visibleCount = sections.Sum(s => s.Photos.Count);
        var status = favoritesOnly
            ? $"Favorites · {visibleCount:N0} photos"
            : folderPath is not null
                ? $"{FormatFolderTitle(folderPath)} · {visibleCount:N0} photos"
                : StatusText;

        return new WebGallerySnapshot(visibleCount, status, IsBusy, sections);
    }

    public IReadOnlyList<WebFolderDto> GetWebFolderTree() =>
        _index.GetFolderTree().Select(MapWebFolder).ToList();

    public void SetPhotoFavorite(string absolutePath, bool favorite)
    {
        absolutePath = Path.GetFullPath(absolutePath);
        var settings = _store.Load();
        var set = settings.FavoritePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (favorite)
            set.Add(absolutePath);
        else
            set.Remove(absolutePath);

        settings.FavoritePaths = set.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        _store.Save(settings);
        LibraryUpdated?.Invoke();
    }

    private HashSet<string> GetFavoritePathSet() =>
        _store.Load().FavoritePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string FormatWebSectionTitle(string headerText)
    {
        var suffixStart = headerText.LastIndexOf(" (", StringComparison.Ordinal);
        if (suffixStart > 0 && headerText.EndsWith(" Photos)", StringComparison.Ordinal))
            return headerText[..suffixStart];

        return headerText;
    }

    private static WebFolderDto MapWebFolder(FolderBrowseNode node) =>
        new(
            node.AbsolutePath,
            node.DisplayName,
            node.PhotoCountTotal,
            node.Children.Select(MapWebFolder).ToList());

    public Task RequestRescanAsync() => ScanLibraryAsync();

    public async Task RequestAddFolderAsync()
    {
        if (AddAccessibleFolderCommand.CanExecute(null))
            await AddAccessibleFolderCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

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

        if (IsFolderAlbumsMode)
            _ = RefreshFolderAlbumsAsync();
        else if (IsPhotoTimelineVisible)
            _ = RefreshPhotoTimelineAsync();
    }

    partial void OnViewModeChanged(LibraryViewMode value)
    {
        OnPropertyChanged(nameof(IsLibraryMode));
        OnPropertyChanged(nameof(IsFolderAlbumsMode));
        OnPropertyChanged(nameof(IsFolderPhotosMode));
        OnPropertyChanged(nameof(IsPhotoTimelineVisible));
        OnPropertyChanged(nameof(IsCenterTimelineVisible));
        OnPropertyChanged(nameof(IsCenterFolderAlbumsVisible));
        OnPropertyChanged(nameof(IsLibraryGridVisible));
        OnPropertyChanged(nameof(IsFolderAlbumGridVisible));
        OnPropertyChanged(nameof(ShowSidebarFolderList));
    }

    partial void OnIsEditModeChanged(bool value)
    {
        if (!value)
            SetInspectorTile(null);

        WorkspaceMode = value ? "Edit" : "Library";
        OnPropertyChanged(nameof(IsEditWorkspace));
        OnPropertyChanged(nameof(IsLibraryGridVisible));
        OnPropertyChanged(nameof(IsFolderAlbumGridVisible));
    }

    partial void OnWorkspaceModeChanged(string value)
    {
        if (value == "Library")
        {
            if (IsEditMode)
                CloseEdit();
            return;
        }

        if (value == "Edit" && !IsEditMode)
        {
            var first = GallerySections.SelectMany(s => s.Photos).FirstOrDefault();
            if (first is not null)
                _ = OpenEditAsync(first);
        }
    }

    partial void OnPreviewZoomPercentChanged(int value)
    {
        if (IsEditMode)
            EditSession.PreviewZoom = Math.Clamp(value / 100.0, 0.25, 4);
    }

    partial void OnSelectedFolderChanged(FolderListItemViewModel? value)
    {
        if (_applyingScanResults || value is null)
            return;

        OpenFolder(value.AbsolutePath, value.Title);
    }

    [RelayCommand]
    private void ApplyEditPreset(string preset)
    {
        var e = EditSession.Edits;
        switch (preset)
        {
            case "original":
                EditSession.ResetEditsCommand.Execute(null);
                break;
            case "bright":
                e.Reset();
                e.Exposure = 0.35;
                e.Contrast = 12;
                e.Shadows = 18;
                e.Highlights = -8;
                e.Saturation = 8;
                break;
            case "cinematic":
                e.Reset();
                e.Exposure = -0.15;
                e.Contrast = 22;
                e.Shadows = 12;
                e.Highlights = -18;
                e.Blacks = -12;
                e.Saturation = -6;
                break;
            case "bw":
                e.Reset();
                e.Contrast = 18;
                e.Saturation = -100;
                break;
            case "warm":
                e.Reset();
                e.Exposure = 0.12;
                e.Saturation = 12;
                e.Vibrance = 10;
                break;
            case "cool":
                e.Reset();
                e.Exposure = 0.05;
                e.Saturation = 6;
                e.Shadows = 8;
                break;
            case "boost":
                e.Reset();
                e.Exposure = 0.28;
                e.Contrast = 16;
                e.Highlights = -12;
                e.Shadows = 22;
                e.Vibrance = 24;
                e.Saturation = 10;
                break;
        }
    }

    [RelayCommand]
    private void ShowLibrary()
    {
        CloseEdit();
        ViewMode = LibraryViewMode.Library;
        OpenFolderPath = null;
        CenterTitle = "Library";
        SelectedFolder = null;
        StatusText = TotalPhotoCount > 0 ? $"Library · {TotalPhotoCount:N0} photos" : "Library";
        _ = RefreshPhotoTimelineAsync();
    }

    [RelayCommand]
    private void ShowFolderAlbums()
    {
        ViewMode = LibraryViewMode.FolderAlbums;
        OpenFolderPath = null;
        CenterTitle = "Folders";
        SelectedFolder = null;
        StatusText = $"{FolderAlbums.Count:N0} folders";
        _ = RefreshFolderAlbumsAsync();
    }

    [RelayCommand]
    private void BackFromFolder()
    {
        if (IsFolderPhotosMode)
            ShowFolderAlbumsCommand.Execute(null);
        else
            ShowLibraryCommand.Execute(null);
    }

    public void OpenFolder(string folderPath, string? title = null)
    {
        folderPath = Path.TrimEndingDirectorySeparator(folderPath);
        ViewMode = LibraryViewMode.FolderPhotos;
        OpenFolderPath = folderPath;
        CenterTitle = title ?? FormatFolderTitle(folderPath);

        SelectedFolder = FolderList.FirstOrDefault(f =>
            string.Equals(f.AbsolutePath, folderPath, StringComparison.OrdinalIgnoreCase));

        var count = _index.GetInFolder(folderPath, recursive: true).Count;
        StatusText = $"{CenterTitle} · {count:N0} photos";
        _ = RefreshPhotoTimelineAsync();
    }

    public void OpenFolderAlbum(FolderAlbumViewModel album) => OpenFolder(album.FolderPath, album.Title);

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
        WorkspaceMode = "Edit";
        PreviewZoomPercent = 100;
        EditSession.ResetPreviewTransform();
        SetInspectorTile(tile);
        var paths = BuildContextPhotoPaths();
        EditSession.SetFilmstripPaths(paths, tile.AbsolutePath);
        await EditSession.LoadPhotoAsync(tile.AbsolutePath, tile.Caption).ConfigureAwait(true);
        StatusText = EditSession.FilmstripPosition.Length > 0
            ? $"{EditSession.FilmstripPosition} — {tile.Caption}"
            : tile.Caption;
    }

    [RelayCommand]
    private void CloseEdit()
    {
        IsEditMode = false;
        EditSession.ResetPreviewTransform();
    }

    public void NavigateEditPhoto(int delta)
    {
        if (!IsEditMode || !EditSession.TryNavigateFilmstrip(delta))
            return;

        SyncInspectorTileFromSession();
        StatusText = $"{EditSession.FilmstripPosition} — {EditSession.FileName}";
    }

    private void SetInspectorTile(PhotoTileViewModel? tile)
    {
        if (_inspectorTile is not null)
            _inspectorTile.IsSelected = false;

        _inspectorTile = tile;

        if (_inspectorTile is not null)
            _inspectorTile.IsSelected = true;
    }

    private void SyncInspectorTileFromSession()
    {
        var path = EditSession.FilePath;
        if (string.IsNullOrEmpty(path))
            return;

        var tile = GallerySections
            .SelectMany(s => s.Photos)
            .FirstOrDefault(t => string.Equals(t.AbsolutePath, path, StringComparison.OrdinalIgnoreCase));

        if (tile is not null)
            SetInspectorTile(tile);
    }

    public void SelectFilmstripTile(PhotoTileViewModel tile)
    {
        if (EditSession.SelectByPath(tile.AbsolutePath))
            StatusText = $"{EditSession.FilmstripPosition} — {tile.Caption}";
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

    private IReadOnlyList<string> BuildContextPhotoPaths()
    {
        if (IsFolderPhotosMode && !string.IsNullOrEmpty(OpenFolderPath))
            return _index.GetInFolder(OpenFolderPath, recursive: true).Select(p => p.AbsolutePath).ToList();

        return _index.GetOrderedPhotoPaths(null);
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
            ClearPhotoTimeline();
            ClearFolderAlbums();
            FolderList.Clear();
            TotalPhotoCount = 0;
            return;
        }

        IsBusy = true;
        StatusText = "Scanning…";
        ClearPhotoTimeline();
        ClearFolderAlbums();

        try
        {
            _index.ScanRoots = roots;
            var progress = new Progress<int>(n => StatusText = $"Scanning… {n:N0} files");

            await _index.RebuildAsync(
                _scanner.ScanAsync(roots, progress),
                CancellationToken.None).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(RebuildFolderList);

            _applyingScanResults = true;
            try
            {
                ViewMode = LibraryViewMode.Library;
                OpenFolderPath = null;
                CenterTitle = "Library";
                SelectedFolder = null;
                await RefreshPhotoTimelineAsync().ConfigureAwait(true);
            }
            finally
            {
                _applyingScanResults = false;
            }

            TotalPhotoCount = _index.TotalPhotoCount();
            StatusText = $"Library · {TotalPhotoCount:N0} photos";
        }
        catch (Exception ex) when (FileSystemPhotoScanner.IsBenignAccessFailure(ex))
        {
            TotalPhotoCount = _index.TotalPhotoCount();
            StatusText = $"Indexed {TotalPhotoCount:N0} photos (some folders skipped).";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            LibraryUpdated?.Invoke();
        }
    }

    private void RebuildFolderList()
    {
        FolderList.Clear();
        var buckets = _index.GetPhotosGroupedByFolder(folderPrefix: null)
            .OrderByDescending(b => b.Photos.Max(p => p.CapturedAt))
            .ThenBy(b => b.FolderPath, StringComparer.OrdinalIgnoreCase);

        foreach (var bucket in buckets)
        {
            FolderList.Add(new FolderListItemViewModel(
                FormatFolderTitle(bucket.FolderPath),
                bucket.FolderPath,
                bucket.Photos.Count));
        }
    }

    private void ClearPhotoTimeline()
    {
        _refreshCts?.Cancel();
        foreach (var section in GallerySections)
        {
            foreach (var tile in section.Photos)
            {
                ThumbnailQueue.Cancel(tile);
                tile.Dispose();
            }
        }

        GallerySections.Clear();
    }

    private void ClearFolderAlbums()
    {
        foreach (var album in FolderAlbums)
            album.Dispose();
        FolderAlbums.Clear();
    }

    private async Task RefreshPhotoTimelineAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;
        var folderFilter = IsFolderPhotosMode ? OpenFolderPath : null;

        var built = await Task.Run(() => BuildGallerySections(folderFilter), token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (token.IsCancellationRequested)
                return;

            ClearPhotoTimeline();
            foreach (var section in built)
                GallerySections.Add(section);

            LibraryUpdated?.Invoke();
        });
    }

    private async Task RefreshFolderAlbumsAsync()
    {
        var built = await Task.Run(BuildFolderAlbums).ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ClearFolderAlbums();
            foreach (var album in built)
                FolderAlbums.Add(album);
        });
    }

    private List<MonthSectionViewModel> BuildGallerySections(
        string? folderPath,
        bool favoritesOnly = false,
        HashSet<string>? favoritePaths = null)
    {
        var query = SearchQuery.Trim();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sections = new List<MonthSectionViewModel>();
        favoritePaths ??= favoritesOnly ? GetFavoritePathSet() : [];

        var buckets = _index.GetPhotosGroupedByFolder(folderPath)
            .OrderByDescending(b => b.Photos.Max(p => p.CapturedAt ?? DateTimeOffset.MinValue))
            .ThenBy(b => b.FolderPath, StringComparer.OrdinalIgnoreCase);

        foreach (var bucket in buckets)
        {
            var groupTiles = new List<PhotoTileViewModel>();

            foreach (var photo in bucket.Photos
                         .OrderByDescending(p => p.CapturedAt ?? DateTimeOffset.MinValue)
                         .ThenBy(p => p.AbsolutePath, StringComparer.OrdinalIgnoreCase))
            {
                if (!seenPaths.Add(photo.AbsolutePath))
                    continue;

                if (favoritesOnly && !favoritePaths.Contains(photo.AbsolutePath))
                    continue;

                var name = Path.GetFileName(photo.AbsolutePath);
                if (!string.IsNullOrEmpty(query) &&
                    name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                groupTiles.Add(new PhotoTileViewModel(photo.AbsolutePath, name));
            }

            if (groupTiles.Count == 0)
                continue;

            var title = FormatFolderSectionTitle(bucket.FolderPath, folderPath);
            var header = $"{title} ({groupTiles.Count:N0} Photos)";
            sections.Add(new MonthSectionViewModel(header, groupTiles));
        }

        return sections;
    }

    private string FormatFolderSectionTitle(string directoryPath, string? selectedRoot)
    {
        if (string.IsNullOrWhiteSpace(selectedRoot))
            return FormatFolderTitle(directoryPath);

        selectedRoot = Path.TrimEndingDirectorySeparator(selectedRoot);
        directoryPath = Path.TrimEndingDirectorySeparator(directoryPath);

        if (string.Equals(directoryPath, selectedRoot, StringComparison.OrdinalIgnoreCase))
            return FormatFolderTitle(directoryPath);

        if (directoryPath.Length > selectedRoot.Length &&
            directoryPath.StartsWith(selectedRoot, StringComparison.OrdinalIgnoreCase) &&
            directoryPath[selectedRoot.Length] == Path.DirectorySeparatorChar)
        {
            var rel = Path.GetRelativePath(selectedRoot, directoryPath);
            if (!string.IsNullOrEmpty(rel) && rel != ".")
                return rel.Replace(Path.DirectorySeparatorChar, '/');
        }

        return FormatFolderTitle(directoryPath);
    }

    private List<FolderAlbumViewModel> BuildFolderAlbums()
    {
        var query = SearchQuery.Trim();
        var albums = new List<FolderAlbumViewModel>();

        foreach (var bucket in _index.GetPhotosGroupedByFolder(folderPrefix: null)
                     .OrderByDescending(b => b.Photos.Max(p => p.CapturedAt))
                     .ThenBy(b => b.FolderPath, StringComparer.OrdinalIgnoreCase))
        {
            var title = FormatFolderTitle(bucket.FolderPath);
            if (!string.IsNullOrEmpty(query) &&
                title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var cover = bucket.Photos.FirstOrDefault()?.AbsolutePath ?? string.Empty;
            if (string.IsNullOrEmpty(cover))
                continue;

            albums.Add(new FolderAlbumViewModel(title, bucket.FolderPath, cover, bucket.Photos.Count));
        }

        return albums;
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

    public void Dispose()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        ThumbnailQueue.Dispose();
        ClearPhotoTimeline();
        ClearFolderAlbums();
    }
}
