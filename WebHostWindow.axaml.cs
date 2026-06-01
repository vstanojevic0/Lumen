using Avalonia.Controls;
using Avalonia.Interactivity;
using Lumen.Services.Catalog;
using Lumen.Services.Scanning;
using Lumen.Services.Settings;
using Lumen.Services.Web;
using Lumen.ViewModels;

namespace Lumen;

public partial class WebHostWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly LumenWebBridge _bridge = new();

    public WebHostWindow()
        : this(new MainWindowViewModel(
            new FileSystemPhotoScanner(),
            new InMemoryLibraryIndex(),
            new JsonAppSettingsStore()))
    {
    }

    public WebHostWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        viewModel.AttachTopLevel(this);
        HostWebView.Source = WebUiSource.Resolve();
        Closed += (_, _) => _bridge.Detach();
    }

    private bool _bridgeInitialized;

    private async void HostWebView_NavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || _bridgeInitialized)
            return;

        _bridgeInitialized = true;
        _bridge.Attach(HostWebView, _viewModel);
        try
        {
            await _bridge.InitializeAsync().ConfigureAwait(true);
        }
        catch
        {
            _viewModel.StatusText = WebUiSource.Hint;
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await _viewModel.InitializeAsync(this).ConfigureAwait(true);
    }
}
