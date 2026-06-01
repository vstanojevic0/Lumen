using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Lumen.Services.Catalog;
using Lumen.Services.Scanning;
using Lumen.Services.Settings;
using Lumen.ViewModels;

namespace Lumen;

public partial class MainWindow : Window
{
    private bool _previewPanning;
    private Point _previewPanStart;
    private double _previewPanOriginX;
    private double _previewPanOriginY;

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
                EditPreviewHost?.Focus();
        };
        viewModel.EditSession.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EditSessionViewModel.PreviewZoom)
                or nameof(EditSessionViewModel.PreviewPanX)
                or nameof(EditSessionViewModel.PreviewPanY))
                UpdatePreviewTransform(viewModel.EditSession);
        };
        viewModel.ScrollToFolderRequested += ScrollToFolderInTimeline;

        EditPreviewHost.PointerWheelChanged += OnEditPreviewWheel;
        EditPreviewHost.PointerPressed += OnEditPreviewPointerPressed;
        EditPreviewHost.PointerMoved += OnEditPreviewPointerMoved;
        EditPreviewHost.PointerReleased += OnEditPreviewPointerReleased;
        EditPreviewHost.KeyDown += OnEditPreviewKeyDown;

        AddHandler(InputElement.KeyDownEvent, OnEditKeyDownTunnel, RoutingStrategies.Tunnel);
        Loaded += OnLoaded;
    }

    private void UpdatePreviewTransform(EditSessionViewModel session)
    {
        EditPreviewViewbox.RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(session.PreviewZoom, session.PreviewZoom),
                new TranslateTransform(session.PreviewPanX, session.PreviewPanY)
            }
        };
    }

    private void ZoomPreviewAtCursor(PointerWheelEventArgs e, EditSessionViewModel session)
    {
        var factor = e.Delta.Y > 0 ? 1.12 : 1 / 1.12;
        var oldZoom = session.PreviewZoom;
        var newZoom = Math.Clamp(oldZoom * factor, 0.25, 8);
        if (Math.Abs(newZoom - oldZoom) < 0.0001)
            return;

        var cursor = e.GetPosition(EditPreviewViewbox);
        var oldPanX = session.PreviewPanX;
        var oldPanY = session.PreviewPanY;

        // screen = pan + local * zoom
        var localX = (cursor.X - oldPanX) / oldZoom;
        var localY = (cursor.Y - oldPanY) / oldZoom;

        session.PreviewZoom = newZoom;
        session.PreviewPanX = cursor.X - localX * newZoom;
        session.PreviewPanY = cursor.Y - localY * newZoom;

        UpdatePreviewTransform(session);
    }

    private void OnEditPreviewWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var session = vm.EditSession;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ZoomPreviewAtCursor(e, session);
            e.Handled = true;
            return;
        }

        if (session.IsCropMode)
            return;

        var delta = e.Delta.Y > 0 ? -1 : 1;
        vm.NavigateEditPhoto(delta);
        e.Handled = true;
    }

    private void OnEditPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var session = vm.EditSession;
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || session.PreviewZoom <= 1.01)
            return;

        _previewPanning = true;
        _previewPanStart = e.GetPosition(EditPreviewViewbox);
        _previewPanOriginX = session.PreviewPanX;
        _previewPanOriginY = session.PreviewPanY;
        e.Pointer.Capture(EditPreviewHost);
        e.Handled = true;
    }

    private void OnEditPreviewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_previewPanning || DataContext is not MainWindowViewModel vm)
            return;

        var pos = e.GetPosition(EditPreviewViewbox);
        vm.EditSession.PreviewPanX = _previewPanOriginX + (pos.X - _previewPanStart.X);
        vm.EditSession.PreviewPanY = _previewPanOriginY + (pos.Y - _previewPanStart.Y);
        e.Handled = true;
    }

    private void OnEditPreviewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _previewPanning = false;
        e.Pointer.Capture(null);
    }

    private void OnEditPreviewKeyDown(object? sender, KeyEventArgs e)
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

    private void ScrollToFolderInTimeline(string folderPath)
    {
        if (TimelineList is null || DataContext is not MainWindowViewModel vm)
            return;

        var index = vm.FindTimelineIndexForFolder(folderPath);
        if (index < 0 || index >= vm.TimelineItems.Count)
            return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            TimelineList.SelectedIndex = index;
            TimelineList.ScrollIntoView(vm.TimelineItems[index]);
        }, Avalonia.Threading.DispatcherPriority.Loaded);
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

        if (sender is not StyledElement { DataContext: PhotoTileViewModel tile })
            return;

        vm.SelectFilmstripTile(tile);
    }
}
