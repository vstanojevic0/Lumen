namespace Lumen.Services.Settings;

public sealed class AppSettings
{
    public bool FirstRunCompleted { get; set; }

    /// <summary>Absolute paths to scan (e.g. Windows drive roots like C:\, D:\).</summary>
    public List<string> ScanRoots { get; set; } = new();
}
