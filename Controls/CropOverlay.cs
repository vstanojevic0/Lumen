using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lumen.ViewModels;

namespace Lumen.Controls;

public partial class CropOverlay : UserControl
{
    private bool _dragging;
    private Point _dragStart;
    private double _startX, _startY;

    public static readonly StyledProperty<EditSessionViewModel?> SessionProperty =
        AvaloniaProperty.Register<CropOverlay, EditSessionViewModel?>(nameof(Session));

    public EditSessionViewModel? Session
    {
        get => GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public CropOverlay()
    {
        ClipToBounds = true;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SessionProperty || change.Property == BoundsProperty)
            InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Session is null || !Session.IsCropMode || Bounds.Width <= 1 || Bounds.Height <= 1)
            return;

        var w = Bounds.Width;
        var h = Bounds.Height;
        var x = Session.CropX * w;
        var y = Session.CropY * h;
        var cw = Session.CropWidth * w;
        var ch = Session.CropHeight * h;

        var dimBrush = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));
        context.FillRectangle(dimBrush, new Rect(0, 0, w, y));
        context.FillRectangle(dimBrush, new Rect(0, y + ch, w, h - y - ch));
        context.FillRectangle(dimBrush, new Rect(0, y, x, ch));
        context.FillRectangle(dimBrush, new Rect(x + cw, y, w - x - cw, ch));

        var cropRect = new Rect(x, y, cw, ch);
        var borderPen = new Pen(Brushes.White, 2);
        context.DrawRectangle(borderPen, cropRect);

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 1);
        context.DrawLine(gridPen, new Point(x + cw / 3, y), new Point(x + cw / 3, y + ch));
        context.DrawLine(gridPen, new Point(x + 2 * cw / 3, y), new Point(x + 2 * cw / 3, y + ch));
        context.DrawLine(gridPen, new Point(x, y + ch / 3), new Point(x + cw, y + ch / 3));
        context.DrawLine(gridPen, new Point(x, y + 2 * ch / 3), new Point(x + cw, y + 2 * ch / 3));

        const double handle = 10;
        var handleBrush = Brushes.White;
        context.FillRectangle(handleBrush, new Rect(x - handle / 2, y - handle / 2, handle, handle));
        context.FillRectangle(handleBrush, new Rect(x + cw - handle / 2, y - handle / 2, handle, handle));
        context.FillRectangle(handleBrush, new Rect(x - handle / 2, y + ch - handle / 2, handle, handle));
        context.FillRectangle(handleBrush, new Rect(x + cw - handle / 2, y + ch - handle / 2, handle, handle));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Session is null || !Session.IsCropMode)
            return;

        _dragging = true;
        _dragStart = e.GetPosition(this);
        _startX = Session.CropX;
        _startY = Session.CropY;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || Session is null || Bounds.Width <= 1 || Bounds.Height <= 1)
            return;

        var pos = e.GetPosition(this);
        var dx = (pos.X - _dragStart.X) / Bounds.Width;
        var dy = (pos.Y - _dragStart.Y) / Bounds.Height;
        Session.CropX = Math.Clamp(_startX + dx, 0, 1 - Session.CropWidth);
        Session.CropY = Math.Clamp(_startY + dy, 0, 1 - Session.CropHeight);
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragging = false;
        e.Pointer.Capture(null);
    }
}
