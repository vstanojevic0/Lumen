using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Lumen.Services.Imaging;

public static class ImageLoader
{
    /// <summary>
    /// Decodes and scales on a worker thread, returns PNG bytes for UI-thread Bitmap creation.
    /// </summary>
    public static byte[]? TryEncodeThumbnailBytes(string absolutePath, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            return null;

        try
        {
            var info = new FileInfo(absolutePath);
            if (info.Length == 0)
                return null;
        }
        catch (Exception ex) when (IsBenign(ex))
        {
            return null;
        }

        // Path-based decode works best on macOS (HEIC / platform codecs).
        var fromPath = TryEncodeFromSkiaBitmap(SKBitmap.Decode(absolutePath), maxWidth);
        if (fromPath is not null)
            return fromPath;

        try
        {
            var bytes = File.ReadAllBytes(absolutePath);
            if (bytes.Length == 0)
                return null;

            using var stream = new MemoryStream(bytes, writable: false);
            using var codec = SKCodec.Create(stream);
            if (codec is not null)
            {
                var fromCodec = TryEncodeFromCodec(codec, maxWidth);
                if (fromCodec is not null)
                    return fromCodec;
            }

            using var fromStream = SKBitmap.Decode(bytes);
            return TryEncodeFromSkiaBitmap(fromStream, maxWidth);
        }
        catch (Exception ex) when (IsBenign(ex))
        {
            return null;
        }
    }

    /// <summary>
    /// Creates an Avalonia bitmap on the UI thread from encoded PNG bytes.
    /// </summary>
    public static Bitmap? CreateBitmapFromPngBytes(byte[] pngBytes)
    {
        if (pngBytes.Length == 0)
            return null;

        try
        {
            using var stream = new MemoryStream(pngBytes, writable: false);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (IsBenign(ex))
        {
            return null;
        }
    }

    /// <summary>
    /// Builds an Avalonia bitmap from Skia RGBA pixels (UI thread).
    /// </summary>
    public static Bitmap? CreateBitmapFromSkia(SKBitmap bitmap)
    {
        if (bitmap.Width <= 0 || bitmap.Height <= 0)
            return null;

        try
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 92);
            if (data is null)
                return null;

            return CreateBitmapFromPngBytes(data.ToArray());
        }
        catch (Exception ex) when (IsBenign(ex))
        {
            return null;
        }
    }

    /// <summary>
    /// UI-thread fallback using Avalonia's decoder.
    /// </summary>
    public static Bitmap? TryDecodeWithAvalonia(string absolutePath, int maxWidth)
    {
        try
        {
            using var stream = File.OpenRead(absolutePath);
            return Bitmap.DecodeToWidth(stream, maxWidth);
        }
        catch (Exception ex) when (IsBenign(ex))
        {
            return null;
        }
    }

    /// <summary>
    /// Convenience helper when already on the UI thread.
    /// </summary>
    public static Bitmap? TryDecode(string absolutePath, int maxWidth)
    {
        var bytes = TryEncodeThumbnailBytes(absolutePath, maxWidth);
        return bytes is null ? null : CreateBitmapFromPngBytes(bytes);
    }

    private static byte[]? TryEncodeFromCodec(SKCodec codec, int maxWidth)
    {
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0)
            return null;

        var (targetW, targetH) = ScaleToMaxWidth(info.Width, info.Height, maxWidth);
        using var destination = new SKBitmap(new SKImageInfo(targetW, targetH, SKColorType.Rgba8888, SKAlphaType.Premul));

        var sampleSize = ComputeSampleSize(info.Width, info.Height, maxWidth);
        var options = new SKCodecOptions(sampleSize);
        var result = codec.GetPixels(destination.Info, destination.GetPixels(), options);

        if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput)
            return EncodePng(destination);

        using var full = SKBitmap.Decode(codec);
        return TryEncodeFromSkiaBitmap(full, maxWidth);
    }

    private static byte[]? TryEncodeFromSkiaBitmap(SKBitmap? bitmap, int maxWidth)
    {
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            return null;

        if (bitmap.Width <= maxWidth)
            return EncodePng(bitmap);

        var (targetW, targetH) = ScaleToMaxWidth(bitmap.Width, bitmap.Height, maxWidth);
        using var resized = bitmap.Resize(new SKImageInfo(targetW, targetH), SKSamplingOptions.Default);
        return resized is null ? null : EncodePng(resized);
    }

    private static byte[]? EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 92);
        return data?.ToArray();
    }

    private static (int Width, int Height) ScaleToMaxWidth(int width, int height, int maxWidth)
    {
        if (width <= maxWidth)
            return (width, height);

        var targetW = maxWidth;
        var targetH = Math.Max(1, (int)Math.Round(height * (maxWidth / (double)width)));
        return (targetW, targetH);
    }

    private static int ComputeSampleSize(int width, int height, int maxWidth)
    {
        var longest = Math.Max(width, height);
        var sample = 1;
        while (longest / sample > maxWidth * 2)
            sample *= 2;
        return sample;
    }

    private static bool IsBenign(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException;
    }
}
