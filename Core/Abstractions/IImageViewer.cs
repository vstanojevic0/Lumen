namespace Lumen.Core.Abstractions;

/// <summary>
/// Single-image viewer with zoom (separate view/window). Future: histogram, color tools, rotation.
/// </summary>
public interface IImageViewer
{
    Task ShowAsync(string absolutePath, CancellationToken cancellationToken = default);
}
