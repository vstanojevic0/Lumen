namespace Lumen.Services.Catalog;

/// <summary>
/// Supported extensions for indexing. Extend RAW suffixes per vendor as needed.
/// </summary>
public static class PhotoFileExtensions
{
    public static readonly HashSet<string> RasterAndRaw = new(StringComparer.OrdinalIgnoreCase)
    {
        // Common raster formats
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff", ".heic", ".heif", ".avif",
        // Common RAW extensions (decoding via dedicated adapters later)
        ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".orf", ".raf", ".rw2", ".dng", ".pef", ".srw", ".3fr"
    };

    public static bool IsPhotoFile(string absolutePath)
    {
        var ext = Path.GetExtension(absolutePath);
        return !string.IsNullOrEmpty(ext) && RasterAndRaw.Contains(ext);
    }
}
