namespace Lumen.Core.Models;

/// <summary>
/// Per-channel luminance distribution (256 bins, values normalized 0–1).
/// </summary>
public sealed class ImageHistogram
{
    public const int BinCount = 256;

    public float[] Red { get; } = new float[BinCount];
    public float[] Green { get; } = new float[BinCount];
    public float[] Blue { get; } = new float[BinCount];

    public static ImageHistogram FromCounts(int[] red, int[] green, int[] blue)
    {
        var hist = new ImageHistogram();
        NormalizeInto(red, hist.Red);
        NormalizeInto(green, hist.Green);
        NormalizeInto(blue, hist.Blue);
        return hist;
    }

    private static void NormalizeInto(int[] counts, float[] target)
    {
        var max = 1;
        foreach (var c in counts)
            max = Math.Max(max, c);

        for (var i = 0; i < BinCount; i++)
            target[i] = counts[i] / (float)max;
    }
}
