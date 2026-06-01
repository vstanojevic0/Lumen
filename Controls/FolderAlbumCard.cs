using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Lumen.Services.Imaging;
using Lumen.ViewModels;

namespace Lumen.Controls;

public class FolderAlbumCard : Border
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is not FolderAlbumViewModel album || string.IsNullOrEmpty(album.CoverPhotoPath))
            return;

        _ = LoadCoverAsync(album);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is FolderAlbumViewModel album)
        {
            album.CoverThumbnail?.Dispose();
            album.CoverThumbnail = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private static async Task LoadCoverAsync(FolderAlbumViewModel album)
    {
        var path = album.CoverPhotoPath;
        var bytes = await Task.Run(() => ImageLoader.TryEncodeThumbnailBytes(path, 320)).ConfigureAwait(false);
        if (bytes is null)
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!string.Equals(album.CoverPhotoPath, path, StringComparison.OrdinalIgnoreCase))
                return;

            album.CoverThumbnail?.Dispose();
            album.CoverThumbnail = ImageLoader.CreateBitmapFromPngBytes(bytes)
                               ?? ImageLoader.TryDecodeWithAvalonia(path, 320);
        });
    }
}
