using Lumen.Services.Database;
using Lumen.Services.Imaging;

namespace Lumen.Services.Cache;

public sealed class ThumbnailCacheService
{
    public const int SmallMaxEdge = 256;
    public const int MediumMaxEdge = 512;

    private readonly SemaphoreSlim _generateGate = new(2, 2);

    public string GetSmallPath(long photoId) =>
        Path.Combine(LumenAppPaths.ThumbnailSmallDirectory, $"{photoId}.jpg");

    public string GetMediumPath(long photoId) =>
        Path.Combine(LumenAppPaths.ThumbnailMediumDirectory, $"{photoId}.jpg");

    public bool HasValidThumbnails(long photoId, string? smallPath, string? mediumPath)
    {
        var expectedSmall = GetSmallPath(photoId);
        var expectedMedium = GetMediumPath(photoId);

        if (!string.Equals(smallPath, expectedSmall, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(mediumPath, expectedMedium, StringComparison.OrdinalIgnoreCase))
            return false;

        return File.Exists(expectedSmall) && File.Exists(expectedMedium);
    }

    public async Task<(string? SmallPath, string? MediumPath)> EnsureThumbnailsAsync(
        long photoId,
        string sourcePath,
        bool forceRegenerate,
        CancellationToken cancellationToken = default)
    {
        var smallPath = GetSmallPath(photoId);
        var mediumPath = GetMediumPath(photoId);

        if (!forceRegenerate && File.Exists(smallPath) && File.Exists(mediumPath))
            return (smallPath, mediumPath);

        await _generateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRegenerate && File.Exists(smallPath) && File.Exists(mediumPath))
                return (smallPath, mediumPath);

            if (!File.Exists(sourcePath))
                return (null, null);

            var smallBytes = await Task.Run(
                () => ImageLoader.TryEncodeJpegBytes(sourcePath, SmallMaxEdge),
                cancellationToken).ConfigureAwait(false);

            if (smallBytes is null || smallBytes.Length == 0)
                return (null, null);

            Directory.CreateDirectory(LumenAppPaths.ThumbnailSmallDirectory);
            await File.WriteAllBytesAsync(smallPath, smallBytes, cancellationToken).ConfigureAwait(false);

            var mediumBytes = await Task.Run(
                () => ImageLoader.TryEncodeJpegBytes(sourcePath, MediumMaxEdge),
                cancellationToken).ConfigureAwait(false);

            if (mediumBytes is null || mediumBytes.Length == 0)
                return (smallPath, null);

            Directory.CreateDirectory(LumenAppPaths.ThumbnailMediumDirectory);
            await File.WriteAllBytesAsync(mediumPath, mediumBytes, cancellationToken).ConfigureAwait(false);

            return (smallPath, mediumPath);
        }
        finally
        {
            _generateGate.Release();
        }
    }
}
