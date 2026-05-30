namespace Lumen.Core.Models;

/// <summary>Normalized crop rectangle (0–1) relative to image bounds.</summary>
public sealed class CropRect
{
    public CropRect(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public CropRect Clone() => new(X, Y, Width, Height);

    public void Clamp()
    {
        X = Math.Clamp(X, 0, 1);
        Y = Math.Clamp(Y, 0, 1);
        Width = Math.Clamp(Width, 0.05, 1);
        Height = Math.Clamp(Height, 0.05, 1);
        if (X + Width > 1) X = 1 - Width;
        if (Y + Height > 1) Y = 1 - Height;
    }
}
