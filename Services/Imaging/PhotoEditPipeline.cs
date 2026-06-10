using Lumen.Core.Models;
using SkiaSharp;

namespace Lumen.Services.Imaging;

/// <summary>
/// Applies basic develop adjustments and crop via Skia (prototype; later GPU + RAW pipeline).
/// </summary>
public static class PhotoEditPipeline
{
    public static SKBitmap? LoadSource(string absolutePath, int maxEdge = 4096)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            return null;

        try
        {
            SKBitmap? decoded = null;

            decoded = SKBitmap.Decode(absolutePath);
            if (decoded is null)
            {
                var bytes = File.ReadAllBytes(absolutePath);
                if (bytes.Length > 0)
                    decoded = SKBitmap.Decode(bytes);
            }

            if (decoded is null)
                return null;

            decoded = EnsureRgba8888(decoded);
            return DownscaleIfNeeded(decoded, maxEdge);
        }
        catch
        {
            return null;
        }
    }

    public static SKBitmap? Render(SKBitmap source, PhotoEditState edits, CropRect? crop)
    {
        using var normalized = EnsureRgba8888(source.Copy());
        if (normalized is null)
            return null;

        using var oriented = ApplyOrientation(normalized, edits.Orientation);
        if (oriented is null)
            return null;

        using var adjusted = ApplyAdjustments(oriented, edits);
        if (adjusted is null)
            return null;

        if (crop is null)
            return adjusted.Copy();

        var rect = ToPixelRect(adjusted, crop);
        if (rect.Width <= 0 || rect.Height <= 0)
            return adjusted.Copy();

        var subset = new SKBitmap();
        return adjusted.ExtractSubset(subset, rect) ? subset : adjusted.Copy();
    }

    public static byte[]? RenderToPngBytes(SKBitmap source, PhotoEditState edits, CropRect? crop)
    {
        using var rendered = Render(source, edits, crop);
        if (rendered is null)
            return null;

        return EncodePng(rendered);
    }

    public static byte[]? EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 92);
        return data?.ToArray();
    }

    private static SKBitmap? ApplyAdjustments(SKBitmap source, PhotoEditState edits)
    {
        if (edits.Exposure == 0 && edits.Contrast == 0 && edits.Highlights == 0 && edits.Shadows == 0 &&
            edits.Whites == 0 && edits.Blacks == 0 && edits.Vibrance == 0 && edits.Saturation == 0 &&
            Math.Abs(edits.Straighten) < 0.01)
            return source.Copy();

        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (surface is null)
            return source.Copy();

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        using var paint = new SKPaint { IsAntialias = true };

        if (Math.Abs(edits.Straighten) > 0.01)
        {
            canvas.Save();
            canvas.RotateDegrees((float)edits.Straighten, source.Width / 2f, source.Height / 2f);
        }

        paint.ColorFilter = BuildColorFilter(edits);
        canvas.DrawBitmap(source, 0, 0, paint);

        if (Math.Abs(edits.Straighten) > 0.01)
            canvas.Restore();

        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static SKBitmap? ApplyOrientation(SKBitmap source, int orientationDegrees)
    {
        orientationDegrees = ((orientationDegrees % 360) + 360) % 360;
        if (orientationDegrees == 0)
            return source.Copy();

        var width = source.Width;
        var height = source.Height;
        var swap = orientationDegrees is 90 or 270;
        var targetWidth = swap ? height : width;
        var targetHeight = swap ? width : height;

        var bitmap = new SKBitmap(new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        canvas.Translate(targetWidth / 2f, targetHeight / 2f);
        canvas.RotateDegrees(orientationDegrees);
        canvas.Translate(-width / 2f, -height / 2f);
        canvas.DrawBitmap(source, 0, 0);
        return bitmap;
    }

    private static SKBitmap EnsureRgba8888(SKBitmap bitmap)
    {
        if (bitmap.ColorType == SKColorType.Rgba8888 && bitmap.AlphaType == SKAlphaType.Premul)
            return bitmap;

        var converted = new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(converted);
        canvas.DrawBitmap(bitmap, 0, 0);
        if (!ReferenceEquals(bitmap, converted))
            bitmap.Dispose();
        return converted;
    }

    private static SKBitmap DownscaleIfNeeded(SKBitmap bitmap, int maxEdge)
    {
        var longest = Math.Max(bitmap.Width, bitmap.Height);
        if (longest <= maxEdge)
            return bitmap;

        var scale = maxEdge / (double)longest;
        var w = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var h = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        var resized = bitmap.Resize(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul), SKSamplingOptions.Default);
        bitmap.Dispose();
        return resized ?? bitmap;
    }

    private static SKColorFilter BuildColorFilter(PhotoEditState edits)
    {
        var exposure = Math.Pow(2, edits.Exposure);
        var contrast = (100 + edits.Contrast) / 100.0;
        var saturation = (100 + edits.Saturation + edits.Vibrance * 0.5) / 100.0;

        var t = (1.0 - contrast) / 2.0 * 255.0;
        var highlight = -edits.Highlights * 0.004;
        var shadow = edits.Shadows * 0.004;
        var white = edits.Whites * 0.003;
        var black = -edits.Blacks * 0.003;
        var offset = (shadow + black + highlight + white) * 255.0;

        var sr = (float)(0.213 + 0.787 * saturation);
        var sg = (float)(0.715 - 0.715 * saturation);
        var sb = (float)(0.072 - 0.072 * saturation);
        var srg = (float)(0.213 - 0.213 * saturation);
        var srb = (float)(0.072 - 0.072 * saturation);
        var sgr = (float)(0.715 - 0.715 * saturation);
        var sgb = (float)(0.715 - 0.715 * saturation);
        var sbr = (float)(0.072 - 0.072 * saturation);
        var sbg = (float)(0.072 - 0.072 * saturation);

        var matrix = new float[]
        {
            (float)(sr * contrast * exposure), (float)(srg * contrast * exposure), (float)(srb * contrast * exposure), 0, (float)(t + offset),
            (float)(sgr * contrast * exposure), (float)(sg * contrast * exposure), (float)(sgb * contrast * exposure), 0, (float)(t + offset),
            (float)(sbr * contrast * exposure), (float)(sbg * contrast * exposure), (float)(sb * contrast * exposure), 0, (float)(t + offset),
            0, 0, 0, 1, 0
        };

        return SKColorFilter.CreateColorMatrix(matrix);
    }

    private static SKRectI ToPixelRect(SKBitmap bitmap, CropRect crop)
    {
        crop.Clamp();
        var x = (int)Math.Round(crop.X * bitmap.Width);
        var y = (int)Math.Round(crop.Y * bitmap.Height);
        var w = (int)Math.Round(crop.Width * bitmap.Width);
        var h = (int)Math.Round(crop.Height * bitmap.Height);
        w = Math.Clamp(w, 1, bitmap.Width - x);
        h = Math.Clamp(h, 1, bitmap.Height - y);
        return new SKRectI(x, y, x + w, y + h);
    }
}
