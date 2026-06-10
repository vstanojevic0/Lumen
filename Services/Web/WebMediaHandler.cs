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
    public const int FullMaxEdge = 16384;

    private const int MaxCacheEntries = 256;
    private const int MaxConcurrentDecodes = 4;
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

    public async Task<MediaBytes?> GetOriginalAsync(
        string absolutePath,
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

        if (!File.Exists(absolutePath))
            return null;

        var ext = Path.GetExtension(absolutePath);
        if (IsBrowserNativeFormat(ext))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(absolutePath, cancellationToken).ConfigureAwait(false);
                if (bytes.Length == 0)
                    return null;

                return new MediaBytes(bytes, MimeForExtension(ext));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        await _decodeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var decodeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            decodeCts.CancelAfter(DecodeTimeout);

            var dims = ImageLoader.TryReadImageDimensions(absolutePath);
            var maxEdge = dims is { Width: var w, Height: var h }
                ? Math.Max(w, h)
                : FullMaxEdge;

            var jpeg = await Task.Run(
                () => ImageLoader.TryEncodeJpegBytes(absolutePath, maxEdge, quality: 92),
                decodeCts.Token).ConfigureAwait(false);

            if (jpeg is null || jpeg.Length == 0)
                return null;

            return new MediaBytes(jpeg, "image/jpeg");
        }
        finally
        {
            _decodeGate.Release();
        }
    }

    private static bool IsBrowserNativeFormat(string ext)
    {
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".avif", StringComparison.OrdinalIgnoreCase);
    }

    private static string MimeForExtension(string ext) =>
        ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".avif" => "image/avif",
            _ => "application/octet-stream",
        };

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
