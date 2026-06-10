namespace Lumen.Core.Models;

public sealed class CatalogPhotoRecord
{
    public long Id { get; init; }
    public long FolderId { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string? Extension { get; init; }
    public long FileSize { get; init; }
    public DateTimeOffset? DateCreated { get; init; }
    public DateTimeOffset DateModified { get; init; }
    public DateTimeOffset? DateTaken { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? Orientation { get; init; }
    public string? CameraModel { get; init; }
    public string? Hash { get; init; }
    public string? ThumbnailPath { get; init; }
    public string? ThumbnailMediumPath { get; init; }
    public bool IsMissing { get; init; }
    public bool IsFavorite { get; init; }
    public int Rating { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public PhotoEntry ToPhotoEntry() =>
        new(
            FilePath,
            DateCreated ?? DateTaken ?? DateModified,
            FileSize,
            Width,
            Height);
}
