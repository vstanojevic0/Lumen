using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lumen.Services.Cache;
using Lumen.Services.Catalog;
using Lumen.Services.Database;
using Lumen.Services.Metadata;
using Lumen.Services.Scanning;
using Lumen.Services.Settings;
using Lumen.Services.Sync;
using Lumen.Services.Web;
using Lumen.ViewModels;

namespace Lumen;

public partial class App : Application
{
    private LumenEmbeddedWebServer? _loopbackServer;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var database = new LocalDatabaseService();
            database.EnsureInitialized();

            var folders = new FolderRepository(database);
            var photos = new PhotoRepository(database);
            var scanState = new ScanStateRepository(database);
            var photoScanner = new PhotoScannerService();
            var catalogRepair = new CatalogRepairService(database, photos, scanState, photoScanner);
            var metadata = new MetadataExtractorService();
            var thumbnails = new ThumbnailCacheService();
            var thumbnailBacklog = new ThumbnailBacklogService(photos, thumbnails);
            thumbnailBacklog.Start();
            var librarySync = new LibrarySyncService(
                database,
                folders,
                photos,
                scanState,
                photoScanner,
                metadata,
                thumbnailBacklog);

            var media = new WebMediaHandler(photos);
            _loopbackServer = LumenEmbeddedWebServer.TryStart(media);
            WebUiSource.MediaBaseUri = _loopbackServer?.BaseUri;

#if !DEBUG
            if (WebUiSource.HasBundledUi)
                WebUiSource.EmbeddedBaseUri = _loopbackServer?.BaseUri;
#endif

            desktop.Exit += (_, _) =>
            {
                thumbnailBacklog.Dispose();
                _loopbackServer?.Dispose();
                _loopbackServer = null;
                WebUiSource.MediaBaseUri = null;
                WebUiSource.EmbeddedBaseUri = null;
            };

            var library = new LibraryViewModel(
                new InMemoryLibraryIndex(),
                new JsonAppSettingsStore(),
                librarySync,
                photos,
                catalogRepair,
                scanState);

            desktop.MainWindow = new WebHostWindow(library);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
