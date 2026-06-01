namespace Lumen.Services.Settings;

public sealed class AppSettings
{
    public bool FirstRunCompleted { get; set; }

    /// <summary>Absolute paths to scan (e.g. Windows drive roots like C:\, D:\).</summary>
    public List<string> ScanRoots { get; set; } = new();

    /// <summary>Absolute paths of photos marked as favorites.</summary>
    public List<string> FavoritePaths { get; set; } = new();
}
