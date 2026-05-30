using Lumen.Core.Models;
using SkiaSharp;

namespace Lumen.Services.Imaging;

public static class HistogramBuilder
{
    public static ImageHistogram? Compute(SKBitmap bitmap, int maxSampleEdge = 480)
    {
        if (bitmap.Width <= 0 || bitmap.Height <= 0)
            return null;

        using var sample = DownscaleForSampling(bitmap, maxSampleEdge);

        var red = new int[ImageHistogram.BinCount];
        var green = new int[ImageHistogram.BinCount];
        var blue = new int[ImageHistogram.BinCount];

        for (var y = 0; y < sample.Height; y++)
        {
            for (var x = 0; x < sample.Width; x++)
            {
                var c = sample.GetPixel(x, y);
                red[c.Red]++;
                green[c.Green]++;
                blue[c.Blue]++;
            }
        }

        return ImageHistogram.FromCounts(red, green, blue);
    }

    private static SKBitmap DownscaleForSampling(SKBitmap bitmap, int maxEdge)
    {
        using var rgba = ToRgba8888(bitmap);
        var longest = Math.Max(rgba.Width, rgba.Height);
        if (longest <= maxEdge)
            return rgba.Copy();

        var scale = maxEdge / (double)longest;
        var w = Math.Max(1, (int)Math.Round(rgba.Width * scale));
        var h = Math.Max(1, (int)Math.Round(rgba.Height * scale));
        var resized = rgba.Resize(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul), SKSamplingOptions.Default);
        return resized ?? rgba.Copy();
    }

    private static SKBitmap ToRgba8888(SKBitmap bitmap)
    {
        if (bitmap.ColorType == SKColorType.Rgba8888 && bitmap.AlphaType == SKAlphaType.Premul)
            return bitmap.Copy();

        var converted = new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(converted);
        canvas.DrawBitmap(bitmap, 0, 0);
        return converted;
    }
}
