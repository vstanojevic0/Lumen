using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumen.Core.Models;

namespace Lumen.Controls;

public class HistogramControl : Control
{
    public static readonly StyledProperty<ImageHistogram?> DataProperty =
        AvaloniaProperty.Register<HistogramControl, ImageHistogram?>(nameof(Data));

    public ImageHistogram? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    static HistogramControl()
    {
        AffectsRender<HistogramControl>(DataProperty, BoundsProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty)
            InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 2 || h < 2)
            return;

        context.FillRectangle(new SolidColorBrush(Color.Parse("#0A0E14")), new Rect(0, 0, w, h));

        var hist = Data;
        if (hist is null)
            return;

        DrawChannel(context, hist.Red, Color.FromArgb(200, 255, 80, 80), w, h);
        DrawChannel(context, hist.Green, Color.FromArgb(200, 80, 220, 120), w, h);
        DrawChannel(context, hist.Blue, Color.FromArgb(200, 80, 140, 255), w, h);
    }

    private static void DrawChannel(DrawingContext context, float[] bins, Color color, double width, double height)
    {
        if (bins.Length < 2)
            return;

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(0, height), true);
            for (var i = 0; i < bins.Length; i++)
            {
                var x = i / (double)(bins.Length - 1) * width;
                var y = height - bins[i] * (height - 2) - 1;
                g.LineTo(new Point(x, y));
            }

            g.LineTo(new Point(width, height));
            g.EndFigure(true);
        }

        context.DrawGeometry(new SolidColorBrush(color), null, geometry);
    }
}
