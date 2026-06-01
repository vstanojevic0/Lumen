using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lumen.Services.Catalog;
using Lumen.Services.Scanning;
using Lumen.Services.Settings;
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
            var media = new WebMediaHandler();
            _loopbackServer = LumenEmbeddedWebServer.TryStart(media);
            WebUiSource.MediaBaseUri = _loopbackServer?.BaseUri;

#if !DEBUG
            if (WebUiSource.HasBundledUi)
                WebUiSource.EmbeddedBaseUri = _loopbackServer?.BaseUri;
#endif

            desktop.Exit += (_, _) =>
            {
                _loopbackServer?.Dispose();
                _loopbackServer = null;
                WebUiSource.MediaBaseUri = null;
                WebUiSource.EmbeddedBaseUri = null;
            };

            var settingsStore = new JsonAppSettingsStore();
            var scanner = new FileSystemPhotoScanner();
            var index = new InMemoryLibraryIndex();
            var mainVm = new MainWindowViewModel(scanner, index, settingsStore);
            var useClassicUi = desktop.Args?.Contains("--classic-ui", StringComparer.OrdinalIgnoreCase) == true
                               || string.Equals(Environment.GetEnvironmentVariable("LUMEN_CLASSIC_UI"), "1", StringComparison.Ordinal);
            desktop.MainWindow = useClassicUi ? new MainWindow(mainVm) : new WebHostWindow(mainVm);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
