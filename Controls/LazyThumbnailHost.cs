using Avalonia;
using Avalonia.Controls;
using Lumen.Services.Imaging;
using Lumen.ViewModels;

// MainWindowViewModel is resolved at runtime from the visual tree root.

namespace Lumen.Controls;

/// <summary>
/// Loads a grid thumbnail when attached; releases bitmap when scrolled off-screen.
/// </summary>
public class LazyThumbnailHost : Border
{
    public static readonly StyledProperty<ThumbnailLoadQueue?> QueueProperty =
        AvaloniaProperty.Register<LazyThumbnailHost, ThumbnailLoadQueue?>(nameof(Queue));

    public ThumbnailLoadQueue? Queue
    {
        get => GetValue(QueueProperty);
        set => SetValue(QueueProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Queue ??= (TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel)?.ThumbnailQueue;
        if (DataContext is PhotoTileViewModel tile && Queue is not null)
            Queue.Request(tile);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is PhotoTileViewModel tile && Queue is not null)
            Queue.Release(tile);
        base.OnDetachedFromVisualTree(e);
    }
}
