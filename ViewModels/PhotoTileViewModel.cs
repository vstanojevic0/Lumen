using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Lumen.Services.Imaging;

namespace Lumen.ViewModels;

public partial class PhotoTileViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] private Bitmap? _thumbnail;

    public PhotoTileViewModel(string absolutePath, string caption)
    {
        AbsolutePath = absolutePath;
        Caption = caption;
    }

    public string AbsolutePath { get; }
    public string Caption { get; }

    [ObservableProperty] private bool _isSelected;

    public async Task LoadThumbnailAsync(CancellationToken cancellationToken = default)
    {
        if (Thumbnail is not null)
            return;

        var path = AbsolutePath;
        var pngBytes = await Task.Run(
            () => ImageLoader.TryEncodeThumbnailBytes(path, 220),
            cancellationToken).ConfigureAwait(false);

        if (pngBytes is null)
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Thumbnail is not null)
                return;

            Thumbnail = pngBytes is not null
                ? ImageLoader.CreateBitmapFromPngBytes(pngBytes)
                : null;

            Thumbnail ??= ImageLoader.TryDecodeWithAvalonia(path, 220);
        });
    }

    public void Dispose()
    {
        Thumbnail?.Dispose();
        Thumbnail = null;
    }
}
