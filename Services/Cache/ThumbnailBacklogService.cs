using Lumen.Services.Database;

namespace Lumen.Services.Cache;

/// <summary>
/// Generates thumbnails in the background so library indexing is not blocked on decode I/O.
/// </summary>
public sealed class ThumbnailBacklogService : IDisposable
{
    private readonly PhotoRepository _photos;
    private readonly ThumbnailCacheService _thumbnails;
    private readonly object _workerLock = new();
    private readonly Queue<ThumbnailJob> _queue = new();
    private CancellationTokenSource _lifetime = new();
    private Task? _worker;

    public ThumbnailBacklogService(PhotoRepository photos, ThumbnailCacheService thumbnails)
    {
        _photos = photos;
        _thumbnails = thumbnails;
    }

    public int PendingCount
    {
        get
        {
            lock (_workerLock)
                return _queue.Count;
        }
    }

    public void Start()
    {
        lock (_workerLock)
        {
            if (_worker is { IsCompleted: false })
                return;

            _worker = Task.Run(ProcessLoopAsync);
        }
    }

    public void Enqueue(long photoId, string sourcePath, bool forceRegenerate = true)
    {
        lock (_workerLock)
        {
            _queue.Enqueue(new ThumbnailJob(photoId, sourcePath, forceRegenerate));
            Start();
        }
    }

    public void EnqueueBatch(IEnumerable<ThumbnailJob> jobs)
    {
        lock (_workerLock)
        {
            foreach (var job in jobs)
                _queue.Enqueue(job);
            Start();
        }
    }

    private async Task ProcessLoopAsync()
    {
        var token = _lifetime.Token;

        while (!token.IsCancellationRequested)
        {
            ThumbnailJob? job = null;
            lock (_workerLock)
            {
                if (_queue.Count > 0)
                    job = _queue.Dequeue();
            }

            if (job is null)
            {
                try
                {
                    await Task.Delay(250, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            var work = job.Value;
            try
            {
                var paths = await _thumbnails.EnsureThumbnailsAsync(
                    work.PhotoId,
                    work.SourcePath,
                    work.ForceRegenerate,
                    token).ConfigureAwait(false);

                if (paths.SmallPath is null)
                    continue;

                _photos.UpdateThumbnailPathsBatch([
                    new ThumbnailPathUpdate
                    {
                        PhotoId = work.PhotoId,
                        SmallPath = paths.SmallPath,
                        MediumPath = paths.MediumPath
                    }
                ]);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
            }
        }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        try
        {
            _worker?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
    }

    public readonly record struct ThumbnailJob(long PhotoId, string SourcePath, bool ForceRegenerate);
}
