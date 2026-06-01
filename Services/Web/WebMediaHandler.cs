using System.Collections.Concurrent;
using Lumen.Services.Imaging;

namespace Lumen.Services.Web;

/// <summary>
/// Bounded, throttled image decode for the embedded web UI (avoids base64 bridge RAM spikes).
/// </summary>
public sealed class WebMediaHandler
{
    public const int ThumbMaxEdge = 180;
    public const int PreviewMaxEdge = 960;

    private const int MaxCacheEntries = 48;
    private const int MaxConcurrentDecodes = 2;

    private readonly SemaphoreSlim _decodeGate = new(MaxConcurrentDecodes, MaxConcurrentDecodes);
    private readonly ConcurrentDictionary<string, byte[]> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheOrderLock = new();
    private readonly Queue<string> _cacheOrder = new();

    public async Task<byte[]?> GetPngAsync(string absolutePath, int maxEdge, CancellationToken cancellationToken = default)
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

        var cacheKey = $"{maxEdge}|{absolutePath}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        await _decodeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(cacheKey, out cached))
                return cached;

            var bytes = await Task.Run(
                () => ImageLoader.TryEncodeThumbnailBytes(absolutePath, maxEdge),
                cancellationToken).ConfigureAwait(false);

            if (bytes is null || bytes.Length == 0)
                return null;

            Remember(cacheKey, bytes);
            return bytes;
        }
        finally
        {
            _decodeGate.Release();
        }
    }

    private void Remember(string key, byte[] bytes)
    {
        _cache[key] = bytes;
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
}
