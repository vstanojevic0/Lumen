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
    private LumenEmbeddedWebServer? _embeddedWeb;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
#if !DEBUG
            _embeddedWeb = LumenEmbeddedWebServer.TryStart();
            WebUiSource.EmbeddedBaseUri = _embeddedWeb?.BaseUri;
            desktop.Exit += (_, _) =>
            {
                _embeddedWeb?.Dispose();
                _embeddedWeb = null;
            };
#endif

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
