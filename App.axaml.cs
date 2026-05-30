using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lumen.Services.Catalog;
using Lumen.Services.Scanning;
using Lumen.Services.Settings;
using Lumen.ViewModels;

namespace Lumen;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsStore = new JsonAppSettingsStore();
            var scanner = new FileSystemPhotoScanner();
            var index = new InMemoryLibraryIndex();
            var mainVm = new MainWindowViewModel(scanner, index, settingsStore);
            desktop.MainWindow = new MainWindow(mainVm);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
