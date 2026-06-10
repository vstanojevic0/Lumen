using System.Security.Cryptography;
using System.Text;
using Lumen.Services.Database;
using Lumen.Services.Imaging;

namespace Lumen.Services.Metadata;

public sealed class MetadataExtractorService
{
    public ExtractedPhotoMetadata? TryExtract(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return null;

        try
        {
            absolutePath = CatalogPathNormalizer.NormalizeFilePath(absolutePath);
            var info = new FileInfo(absolutePath);
            if (!info.Exists || info.Length == 0)
                return null;

            var dateCreated = new DateTimeOffset(info.CreationTimeUtc, TimeSpan.Zero);
            var dateModified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
            var dateTaken = dateModified;
            var dimensions = TryReadDimensions(absolutePath);

            return new ExtractedPhotoMetadata
            {
                FilePath = absolutePath,
                FileName = info.Name,
                Extension = Path.GetExtension(absolutePath),
                FileSize = info.Length,
                DateCreated = dateCreated,
                DateModified = dateModified,
                DateTaken = dateTaken,
                Width = dimensions?.Width,
                Height = dimensions?.Height,
                Hash = ComputeFingerprint(absolutePath, info.Length, dateModified)
            };
        }
        catch (Exception ex) when (IsBenign(ex))
        {
            return null;
        }
    }

    public static string ComputeFingerprint(string filePath, long fileSize, DateTimeOffset dateModified)
    {
        var payload = $"{filePath}|{fileSize}|{dateModified.UtcTicks}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private static (int Width, int Height)? TryReadDimensions(string absolutePath)
    {
        try
        {
            using var codec = SkiaSharp.SKCodec.Create(absolutePath);
            if (codec is null)
                return ImageLoader.TryReadImageDimensions(absolutePath);

            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0)
                return null;

            return (info.Width, info.Height);
        }
        catch (Exception ex) when (IsBenign(ex))
        {
            return null;
        }
    }

    private static bool IsBenign(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException;
}

public sealed class ExtractedPhotoMetadata
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string? Extension { get; init; }
    public long FileSize { get; init; }
    public DateTimeOffset? DateCreated { get; init; }
    public DateTimeOffset DateModified { get; init; }
    public DateTimeOffset? DateTaken { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public string Hash { get; init; } = string.Empty;
}
