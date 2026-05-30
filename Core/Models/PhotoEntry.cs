namespace Lumen.Core.Models;

/// <summary>
/// One catalog entry. Path is canonical; other fields come from EXIF/metadata and caches.
/// Future: FaceRegionIds, EditStackId, CloudRemoteId.
/// </summary>
public sealed record PhotoEntry(
    string AbsolutePath,
    DateTimeOffset? CapturedAt,
    long FileSizeBytes,
    int? Width,
    int? Height);
