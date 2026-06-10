using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Core.Models;
using Lumen.Services.Catalog;
using Lumen.Services.Database;
using Lumen.Services.Library;
using Lumen.Services.Scanning;
using Lumen.Services.Settings;
using Lumen.Services.Sync;
using Lumen.Services.Web;

namespace Lumen.ViewModels;

public sealed partial class LibraryViewModel : ObservableObject, IDisposable
{
    private readonly InMemoryLibraryIndex _index;
    private readonly IAppSettingsStore _store;
    private readonly LibrarySyncService _sync;
    private readonly PhotoRepository _photos;
    private readonly CatalogRepairService _catalogRepair;
    private readonly ScanStateRepository _scanState;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private TopLevel? _topLevel;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Preparing library…";
    [ObservableProperty] private int _totalPhotoCount;

    public event Action? LibraryUpdated;

    public LibraryViewModel(
        InMemoryLibraryIndex index,
        IAppSettingsStore store,
        LibrarySyncService sync,
        PhotoRepository photos,
        CatalogRepairService catalogRepair,
        ScanStateRepository scanState)
    {
        _index = index;
        _store = store;
        _sync = sync;
        _photos = photos;
        _catalogRepair = catalogRepair;
        _scanState = scanState;
    }

    public void AttachTopLevel(TopLevel topLevel) => _topLevel = topLevel;

    public WebStatusDto GetWebStatus() =>
        new(TotalPhotoCount, StatusText, IsBusy, GetFavoritePathSet().Count, WebUiSource.MediaBaseUri?.ToString());

    public WebGallerySnapshot GetWebGallerySnapshot(string? folderPath = null, bool favoritesOnly = false)
    {
        var favorites = GetFavoritePathSet();
        var sections = BuildGallerySections(folderPath, favoritesOnly, favorites)
            .Select(s => new WebGallerySectionDto(
                FormatWebSectionTitle(s.Title),
                s.FolderPath,
                s.Photos.Select(p => new WebPhotoDto(p.Path, p.Title, favorites.Contains(p.Path))).ToList()))
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
        _photos.SetFavorite(absolutePath, favorite);
        LibraryUpdated?.Invoke();
    }

    public Task RequestRescanAsync() => ScanLibraryAsync(fullRescan: true);

    public async Task RequestAddFolderAsync()
    {
        if (AddAccessibleFolderCommand.CanExecute(null))
            await AddAccessibleFolderCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    private bool _initialized;

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;

        var loaded = _store.Load();
        if (loaded.ScanRoots.Count == 0)
            ApplyFallbackRootsFromCatalog();

        loaded = _store.Load();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            EnsureMacStartupLibrary();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            EnsureWindowsStartupLibrary();

        loaded = _store.Load();
        PruneOverlappingScanRoots(loaded);
        MigrateFavoritesToDatabase(loaded);

        await Task.Run(() => _catalogRepair.RepairCatalog()).ConfigureAwait(true);

        await LoadLibraryFromDatabaseAsync().ConfigureAwait(true);
        _ = RunBackgroundSyncAsync(fullRescan: false);
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
            Title = "Add a folder to your library",
            AllowMultiple = false
        }).ConfigureAwait(true);

        if (folders.Count == 0)
            return;

        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        path = Path.TrimEndingDirectorySeparator(path);
        var settings = _store.Load();
        if (!settings.ScanRoots.Any(r =>
                string.Equals(Path.TrimEndingDirectorySeparator(r), path, StringComparison.OrdinalIgnoreCase)))
        {
            settings.ScanRoots.Add(path);
        }

        settings.FirstRunCompleted = true;
        _store.Save(settings);
        await ScanLibraryAsync(fullRescan: false).ConfigureAwait(true);
    }

    private async Task LoadLibraryFromDatabaseAsync()
    {
        var roots = GetActiveScanRoots();

        if (roots.Count == 0)
        {
            StatusText = "Add a folder to get started.";
            TotalPhotoCount = 0;
            LibraryUpdated?.Invoke();
            return;
        }

        try
        {
            await _sync.ReloadIndexAsync(_index, roots, _lifetimeCts.Token).ConfigureAwait(true);
            TotalPhotoCount = _index.TotalPhotoCount();
            StatusText = TotalPhotoCount > 0
                ? $"Library · {TotalPhotoCount:N0} photos"
                : "Checking for photos…";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading library: {ex.Message}";
            TotalPhotoCount = 0;
        }

        LibraryUpdated?.Invoke();
    }

    private async Task RunBackgroundSyncAsync(bool fullRescan, bool isRecoveryAttempt = false)
    {
        var roots = GetActiveScanRoots();
        if (roots.Count == 0)
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsBusy = true;
            StatusText = fullRescan ? "Scanning library…" : "Checking for changes…";
        });

        try
        {
            if (!isRecoveryAttempt)
            {
                await Dispatcher.UIThread.InvokeAsync(() => StatusText = "Preparing library…");

                var plan = await Task.Run(
                    () => _catalogRepair.AssessSyncPlan(roots, _lifetimeCts.Token),
                    _lifetimeCts.Token).ConfigureAwait(false);

                if (plan.NeedsFullRescan)
                {
                    fullRescan = true;
                    var preparing = FormatPreparingStatus(plan.Reason);
                    await Dispatcher.UIThread.InvokeAsync(() => StatusText = preparing);
                }
            }
            else
            {
                _catalogRepair.RepairCatalog();
                fullRescan = true;
                await Dispatcher.UIThread.InvokeAsync(() => StatusText = "Fixing library index…");
            }

            var progress = new Progress<SyncProgress>(p =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    TotalPhotoCount = _index.TotalPhotoCount();
                    StatusText = p.Phase switch
                    {
                        "Sync complete" => $"Library · {TotalPhotoCount:N0} photos",
                        _ => p.ProcessedFiles > 0
                            ? $"{p.Phase} {p.ProcessedFiles:N0} files"
                            : p.Phase
                    };

                    if (p.Phase.StartsWith("Updated ", StringComparison.Ordinal) ||
                        p.Phase == "Sync complete" ||
                        (p.ProcessedFiles > 0 && p.ProcessedFiles % 2000 == 0))
                        LibraryUpdated?.Invoke();
                });
            });

            await _sync.RunIncrementalSyncAsync(
                _index,
                roots,
                fullRescan,
                progress,
                _lifetimeCts.Token).ConfigureAwait(false);

            _scanState.ClearLastSyncError();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                TotalPhotoCount = _index.TotalPhotoCount();
                if (!fullRescan && TotalPhotoCount > 0)
                    StatusText = $"Library · {TotalPhotoCount:N0} photos";
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (FileSystemPhotoScanner.IsBenignAccessFailure(ex))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                TotalPhotoCount = _index.TotalPhotoCount();
                StatusText = $"Indexed {TotalPhotoCount:N0} photos (some folders skipped).";
            });
        }
        catch (Exception ex)
        {
            _scanState.SetLastSyncError(ex.Message);

            if (!isRecoveryAttempt)
            {
                await RunBackgroundSyncAsync(fullRescan: true, isRecoveryAttempt: true);
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
                StatusText = $"Could not finish indexing all photos ({TotalPhotoCount:N0} loaded so far).");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = false;
                LibraryUpdated?.Invoke();
            });
        }
    }

    private static string FormatPreparingStatus(string? reason) =>
        reason switch
        {
            "finishing library index" => "Indexing your photos…",
            "recovering from sync error" => "Fixing library index…",
            "catalog repaired" => "Indexing your photos…",
            "empty catalog" => "Scanning library…",
            _ => "Scanning library…",
        };

    private async Task ScanLibraryAsync(bool fullRescan)
    {
        var roots = GetActiveScanRoots();

        if (roots.Count == 0)
        {
            StatusText = "Add a folder to get started.";
            TotalPhotoCount = 0;
            LibraryUpdated?.Invoke();
            return;
        }

        await RunBackgroundSyncAsync(fullRescan).ConfigureAwait(true);
    }

    private List<string> GetActiveScanRoots() =>
        CatalogPathNormalizer.PruneNestedScanRoots(
            _store.Load().ScanRoots.Where(Directory.Exists));

    private void MigrateFavoritesToDatabase(AppSettings settings)
    {
        if (settings.FavoritePaths.Count == 0)
            return;

        _photos.ApplyFavoriteMigration(settings.FavoritePaths);
    }

    private List<GallerySection> BuildGallerySections(
        string? folderPath,
        bool favoritesOnly,
        HashSet<string> favoritePaths)
    {
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sections = new List<GallerySection>();

        var scanRoots = _index.ScanRoots
            .Select(Path.TrimEndingDirectorySeparator)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var bucket in _index.GetPhotosGroupedByFolder(folderPath)
                     .OrderBy(b => GetScanRootSortKey(b.FolderPath, scanRoots))
                     .ThenBy(b => b.FolderPath, StringComparer.OrdinalIgnoreCase))
        {
            var photos = new List<GalleryPhoto>();

            foreach (var photo in bucket.Photos
                         .OrderByDescending(p => p.CapturedAt ?? DateTimeOffset.MinValue)
                         .ThenBy(p => p.AbsolutePath, StringComparer.OrdinalIgnoreCase))
            {
                if (!seenPaths.Add(photo.AbsolutePath))
                    continue;

                if (favoritesOnly && !favoritePaths.Contains(photo.AbsolutePath))
                    continue;

                photos.Add(new GalleryPhoto(photo.AbsolutePath, Path.GetFileName(photo.AbsolutePath)));
            }

            if (photos.Count == 0)
                continue;

            var title = FormatFolderSectionTitle(bucket.FolderPath, folderPath);
            sections.Add(new GallerySection(
                $"{title} ({photos.Count:N0} Photos)",
                bucket.FolderPath,
                photos));
        }

        return sections;
    }

    private HashSet<string> GetFavoritePathSet()
    {
        var fromDb = _photos.GetFavoritePaths();
        if (fromDb.Count > 0)
            return fromDb.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _store.Load().FavoritePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static int GetScanRootSortKey(string folderPath, IReadOnlyList<string> scanRoots)
    {
        folderPath = Path.TrimEndingDirectorySeparator(folderPath);

        for (var i = 0; i < scanRoots.Count; i++)
        {
            var root = Path.TrimEndingDirectorySeparator(scanRoots[i]);
            if (!folderPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                continue;

            if (folderPath.Length == root.Length || folderPath[root.Length] == Path.DirectorySeparatorChar)
                return i;
        }

        return scanRoots.Count;
    }

    private static WebFolderDto MapWebFolder(FolderBrowseNode node) =>
        new(node.AbsolutePath, node.DisplayName, node.PhotoCountDirect, node.Children.Select(MapWebFolder).ToList());

    private static string FormatWebSectionTitle(string headerText)
    {
        var suffixStart = headerText.LastIndexOf(" (", StringComparison.Ordinal);
        if (suffixStart > 0 && headerText.EndsWith(" Photos)", StringComparison.Ordinal))
            return headerText[..suffixStart];

        return headerText;
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

    private void PruneOverlappingScanRoots(AppSettings settings)
    {
        var pruned = CatalogPathNormalizer.PruneNestedScanRoots(settings.ScanRoots);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            pruned = PruneMacPicturesHomeOverlap(pruned);

        if (pruned.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(settings.ScanRoots.Select(CatalogPathNormalizer.NormalizeFolderPath)))
            return;

        settings.ScanRoots = pruned;
        settings.FirstRunCompleted = true;
        _store.Save(settings);
    }

    private static List<string> PruneMacPicturesHomeOverlap(List<string> roots)
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrWhiteSpace(pictures))
            return roots;

        pictures = CatalogPathNormalizer.NormalizeFolderPath(pictures);
        var home = CatalogPathNormalizer.NormalizeFolderPath(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        if (!roots.Any(p => CatalogPathNormalizer.PathsEqual(p, pictures)))
            return roots;

        return roots
            .Where(p => !CatalogPathNormalizer.PathsEqual(p, home))
            .ToList();
    }

    private void ApplyFallbackRootsFromCatalog()
    {
        var roots = LibraryLocationCatalog.GetSuggestedScanCandidates()
            .Select(c => Path.TrimEndingDirectorySeparator(c.AbsolutePath))
            .ToList();

        var settings = _store.Load();
        settings.FirstRunCompleted = true;
        settings.ScanRoots = roots;
        _store.Save(settings);
    }

    private void EnsureWindowsStartupLibrary()
    {
        var settings = _store.Load();
        if (!settings.ScanEntireComputer)
            return;

        var autoRoots = LibraryLocationCatalog.GetWindowsAutoLoadRoots()
            .Select(c => CatalogPathNormalizer.NormalizeFolderPath(c.AbsolutePath))
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (autoRoots.Count == 0)
            return;

        var merged = settings.ScanRoots
            .Select(CatalogPathNormalizer.NormalizeFolderPath)
            .Concat(autoRoots)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        merged = CatalogPathNormalizer.PruneNestedScanRoots(merged);

        if (merged.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(settings.ScanRoots.Select(CatalogPathNormalizer.NormalizeFolderPath)))
            return;

        settings.ScanRoots = merged;
        settings.FirstRunCompleted = true;
        settings.ScanEntireComputer = true;
        _store.Save(settings);
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

        var settings = _store.Load();
        var merged = settings.ScanRoots
            .Select(Path.TrimEndingDirectorySeparator)
            .Concat(autoRoots)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (merged.Count == 0)
            merged = autoRoots;

        settings.FirstRunCompleted = true;
        settings.ScanRoots = merged;
        _store.Save(settings);
    }

    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
    }

    private sealed record GalleryPhoto(string Path, string Title);

    private sealed record GallerySection(string Title, string FolderPath, List<GalleryPhoto> Photos);
}
