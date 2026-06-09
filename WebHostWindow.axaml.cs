using Avalonia.Controls;
using Avalonia.Interactivity;
using Lumen.Services.Web;
using Lumen.ViewModels;

namespace Lumen;

public partial class WebHostWindow : Window
{
    private readonly LibraryViewModel _library;
    private readonly LumenWebBridge _bridge = new();
    private bool _bridgeInitialized;

    public WebHostWindow(LibraryViewModel library)
    {
        _library = library;
        InitializeComponent();
        library.AttachTopLevel(this);
        HostWebView.Source = WebUiSource.Resolve();
        Closed += (_, _) => _bridge.Detach();
    }

    private async void HostWebView_NavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || _bridgeInitialized)
            return;

        _bridgeInitialized = true;
        _bridge.Attach(HostWebView, _library);
        try
        {
            await _bridge.InitializeAsync().ConfigureAwait(true);
        }
        catch
        {
            _library.StatusText = WebUiSource.Hint;
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await _library.InitializeAsync().ConfigureAwait(true);
    }
}
