using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Lumen.Services.Catalog;
using Lumen.Services.Scanning;
using Lumen.Services.Settings;
using Lumen.ViewModels;

namespace Lumen;

public partial class MainWindow : Window
{
    public MainWindow()
        : this(new MainWindowViewModel(new FileSystemPhotoScanner(), new InMemoryLibraryIndex(), new JsonAppSettingsStore()))
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.AttachTopLevel(this);

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsEditMode) && viewModel.IsEditMode)
                EditCanvasHost?.Focus();
        };

        viewModel.EditSession.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EditSessionViewModel.DisplayImage)
                or nameof(EditSessionViewModel.PreviewZoom)
                or nameof(EditSessionViewModel.PreviewPanX)
                or nameof(EditSessionViewModel.PreviewPanY))
            {
                SyncEditPreview(viewModel.EditSession);
            }
        };

        EditCanvasHost.PointerWheelChanged += OnEditCanvasWheel;
        EditCanvasHost.KeyDown += OnEditCanvasKeyDown;

        AddHandler(InputElement.KeyDownEvent, OnEditKeyDownTunnel, RoutingStrategies.Tunnel);
        Loaded += OnLoaded;
    }

    private void SyncEditPreview(EditSessionViewModel session)
    {
        if (EditPreviewImage is null)
            return;

        EditPreviewImage.Source = session.DisplayImage;
        EditPreviewImage.RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(session.PreviewZoom, session.PreviewZoom),
                new TranslateTransform(session.PreviewPanX, session.PreviewPanY)
            }
        };
    }

    private void OnEditCanvasWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.IsEditMode)
            return;

        var session = vm.EditSession;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var factor = e.Delta.Y > 0 ? 1.1 : 1 / 1.1;
            var pct = (int)Math.Clamp(vm.PreviewZoomPercent * factor, 25, 400);
            vm.PreviewZoomPercent = pct;
            e.Handled = true;
            return;
        }

        if (session.IsCropMode)
            return;

        vm.NavigateEditPhoto(e.Delta.Y > 0 ? -1 : 1);
        e.Handled = true;
    }

    private void OnEditCanvasKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleEditNavigationKey(e))
            e.Handled = true;
    }

    private void OnEditKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (HandleEditNavigationKey(e))
            e.Handled = true;
    }

    private bool HandleEditNavigationKey(KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.IsEditMode)
            return false;

        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                vm.NavigateEditPhoto(-1);
                return true;
            case Key.Right:
            case Key.Down:
                vm.NavigateEditPhoto(1);
                return true;
            case Key.Escape:
                if (vm.EditSession.IsCropMode)
                    vm.EditSession.IsCropMode = false;
                else
                    vm.CloseEditCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }

    private void FolderAlbum_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (sender is StyledElement { DataContext: FolderAlbumViewModel album })
            vm.OpenFolderAlbum(album);
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            await vm.InitializeAsync(this).ConfigureAwait(true);
    }

    private void PhotoTile_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (sender is StyledElement { DataContext: PhotoTileViewModel tile })
            vm.OpenEditCommand.Execute(tile);
    }

    private void Filmstrip_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (sender is StyledElement { DataContext: PhotoTileViewModel tile })
            vm.SelectFilmstripTile(tile);
    }
}
