namespace Lumen.Core.Abstractions;

/// <summary>
/// Cached thumbnails (and optionally mid-res previews) for the grid, on disk or in SQLite blobs.
/// RAW files should be decoded through a dedicated pipeline before scaling.
/// </summary>
public interface IThumbnailCache
{
    Task<Stream?> TryGetAsync(string absolutePath, int maxEdgePixels, CancellationToken cancellationToken = default);

    Task PutAsync(string absolutePath, int maxEdgePixels, ReadOnlyMemory<byte> jpegBytes, CancellationToken cancellationToken = default);
}
