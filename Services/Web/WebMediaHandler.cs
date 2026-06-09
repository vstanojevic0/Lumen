using System.Collections.Concurrent;
using Lumen.Services.Cache;
using Lumen.Services.Database;
using Lumen.Services.Imaging;

namespace Lumen.Services.Web;

/// <summary>
/// Bounded, throttled image decode for the embedded web UI (avoids base64 bridge RAM spikes).
/// Prefers on-disk cached thumbnails from SQLite when available.
/// </summary>
public sealed class WebMediaHandler
{
    public const int ThumbMaxEdge = 180;
    public const int PreviewMaxEdge = 1280;

    private const int MaxCacheEntries = 48;
    private const int MaxConcurrentDecodes = 2;
    private static readonly TimeSpan DecodeTimeout = TimeSpan.FromSeconds(45);

    private readonly PhotoRepository _photos;
    private readonly SemaphoreSlim _decodeGate = new(MaxConcurrentDecodes, MaxConcurrentDecodes);
    private readonly ConcurrentDictionary<string, CachedMedia> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheOrderLock = new();
    private readonly Queue<string> _cacheOrder = new();

    public WebMediaHandler(PhotoRepository photos) => _photos = photos;

    public async Task<MediaBytes?> GetMediaAsync(
        string absolutePath,
        int maxEdge,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return null;

        try
        {
            absolutePath = Path.GetFullPath(absolutePath);
        }
        catch
        {
            return null;
        }

        var cacheKey = $"{maxEdge}|{absolutePath}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached.Bytes;

        var fromDisk = TryReadCachedThumbnail(absolutePath, maxEdge);
        if (fromDisk is not null)
        {
            Remember(cacheKey, fromDisk);
            return fromDisk;
        }

        if (!File.Exists(absolutePath))
            return null;

        await _decodeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(cacheKey, out cached))
                return cached.Bytes;

            fromDisk = TryReadCachedThumbnail(absolutePath, maxEdge);
            if (fromDisk is not null)
            {
                Remember(cacheKey, fromDisk);
                return fromDisk;
            }

            using var decodeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            decodeCts.CancelAfter(DecodeTimeout);

            var bytes = await Task.Run(
                () => ImageLoader.TryEncodeThumbnailBytes(absolutePath, maxEdge),
                decodeCts.Token).ConfigureAwait(false);

            if (bytes is null || bytes.Length == 0)
                return null;

            var media = new MediaBytes(bytes, "image/png");
            Remember(cacheKey, media);
            return media;
        }
        finally
        {
            _decodeGate.Release();
        }
    }

    public Task<byte[]?> GetPngAsync(string absolutePath, int maxEdge, CancellationToken cancellationToken = default) =>
        GetMediaAsync(absolutePath, maxEdge, cancellationToken).ContinueWith(
            t => t.Result?.ContentType == "image/png" ? t.Result.Bytes : t.Result?.Bytes,
            cancellationToken);

    private MediaBytes? TryReadCachedThumbnail(string absolutePath, int maxEdge)
    {
        var photo = _photos.GetByPath(absolutePath);
        if (photo is null || photo.IsMissing)
            return null;

        var cachePath = maxEdge > ThumbnailCacheService.SmallMaxEdge
            ? photo.ThumbnailMediumPath ?? photo.ThumbnailPath
            : photo.ThumbnailPath;

        if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath))
            return null;

        try
        {
            var bytes = File.ReadAllBytes(cachePath);
            if (bytes.Length == 0)
                return null;

            var contentType = cachePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
                ? "image/webp"
                : "image/jpeg";

            return new MediaBytes(bytes, contentType);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void Remember(string key, MediaBytes bytes)
    {
        _cache[key] = new CachedMedia(bytes);
        lock (_cacheOrderLock)
        {
            _cacheOrder.Enqueue(key);
            while (_cacheOrder.Count > MaxCacheEntries)
            {
                var oldest = _cacheOrder.Dequeue();
                _cache.Remove(oldest, out _);
            }
        }
    }

    public static string EncodePathParameter(string absolutePath)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(absolutePath))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string? DecodePathParameter(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            return null;

        try
        {
            var padded = encoded.Replace('-', '+').Replace('_', '/');
            padded += (padded.Length % 4) switch
            {
                2 => "==",
                3 => "=",
                _ => string.Empty
            };

            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        }
        catch
        {
            return null;
        }
    }

    private readonly record struct CachedMedia(MediaBytes Bytes);
}

public sealed class MediaBytes
{
    public MediaBytes(byte[] bytes, string contentType)
    {
        Bytes = bytes;
        ContentType = contentType;
    }

    public byte[] Bytes { get; }
    public string ContentType { get; }
}
