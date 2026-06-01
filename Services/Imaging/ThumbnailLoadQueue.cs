using System.Collections.Concurrent;
using Avalonia.Threading;
using Lumen.ViewModels;

namespace Lumen.Services.Imaging;

/// <summary>
/// Bounded background thumbnail decoding; only visible tiles should enqueue.
/// </summary>
public sealed class ThumbnailLoadQueue : IDisposable
{
    private const int MaxConcurrent = 2;
    private const int MaxEdge = 220;

    private readonly SemaphoreSlim _gate = new(MaxConcurrent, MaxConcurrent);
    private readonly ConcurrentDictionary<PhotoTileViewModel, byte> _pending = new();
    private readonly CancellationTokenSource _shutdown = new();

    public void Request(PhotoTileViewModel tile)
    {
        if (tile.Thumbnail is not null || !_pending.TryAdd(tile, 0))
            return;

        _ = ProcessAsync(tile);
    }

    public void Cancel(PhotoTileViewModel tile)
    {
        _pending.TryRemove(tile, out _);
    }

    public void Release(PhotoTileViewModel tile)
    {
        Cancel(tile);
        Dispatcher.UIThread.Post(() =>
        {
            tile.Thumbnail?.Dispose();
            tile.Thumbnail = null;
        });
    }

    private async Task ProcessAsync(PhotoTileViewModel tile)
    {
        try
        {
            await _gate.WaitAsync(_shutdown.Token).ConfigureAwait(false);
            if (!_pending.ContainsKey(tile) || tile.Thumbnail is not null)
                return;

            var path = tile.AbsolutePath;
            var pngBytes = await Task.Run(
                () => ImageLoader.TryEncodeThumbnailBytes(path, MaxEdge),
                _shutdown.Token).ConfigureAwait(false);

            if (pngBytes is null || !_pending.ContainsKey(tile))
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_pending.ContainsKey(tile) || tile.Thumbnail is not null)
                    return;

                tile.Thumbnail = ImageLoader.CreateBitmapFromPngBytes(pngBytes)
                               ?? ImageLoader.TryDecodeWithAvalonia(path, MaxEdge);
            });
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _pending.TryRemove(tile, out _);
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
        _gate.Dispose();
        _pending.Clear();
    }
}
