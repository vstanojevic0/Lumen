namespace Lumen.Services.Database;

/// <summary>
/// Platform-specific application data and cache directories.
/// </summary>
public static class LumenAppPaths
{
    public static string AppDataRoot
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = AppContext.BaseDirectory;

            return Path.Combine(root, "Lumen");
        }
    }

    public static string DatabasePath => Path.Combine(AppDataRoot, "lumen.db");

    public static string ThumbnailCacheRoot => Path.Combine(AppDataRoot, "Cache", "Thumbnails");

    public static string ThumbnailSmallDirectory => Path.Combine(ThumbnailCacheRoot, "s");

    public static string ThumbnailMediumDirectory => Path.Combine(ThumbnailCacheRoot, "m");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(ThumbnailSmallDirectory);
        Directory.CreateDirectory(ThumbnailMediumDirectory);
    }
}
