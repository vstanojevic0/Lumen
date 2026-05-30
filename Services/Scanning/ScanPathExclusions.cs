namespace Lumen.Services.Scanning;

/// <summary>
/// Filters out system folders, app bundles, and common non-photo asset trees.
/// </summary>
public static class ScanPathExclusions
{
    private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // macOS / Unix system & metadata
        ".Trash", ".Trashes", "Trash", ".Trash-1000", ".Spotlight-V100", ".TemporaryItems",
        ".DocumentRevisions-V100", ".fseventsd", "lost+found", ".cache", ".npm", ".nuget",
        ".vscode", ".cursor", ".git", ".svn", ".hg",

        // User/system libraries (not photo libraries under Pictures)
        "Library", "Application Support", "Caches", "Cache", "Logs", "Log", "Temp", "tmp",
        "Intents", "Containers", "Group Containers", "Saved Application State",
        "Developer", "Xcode", "DerivedData", "CoreSimulator",

        // Windows system
        "Windows", "Program Files", "Program Files (x86)", "ProgramData", "AppData",
        "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs",
        "Microsoft", "Packages", "WinSxS", "Servicing",

        // Dev / package managers
        "node_modules", "bower_components", "vendor", "packages", "PackageCache",
        "bin", "obj", "build", "dist", "out", "target", "gradle", ".gradle",

        // Game / engine asset trees
        "Steam", "steamapps", "Epic Games", "GOG Galaxy", "Unity", "UnrealEngine",
        "Unreal Engine", "Blender", "Godot", "RPG Maker", "World of Warcraft",
        "Assets", "AssetBundles", "StreamingAssets", "Resources", "Art", "Artwork",
        "Textures", "textures", "Sprites", "sprites", "Icons", "icons", "UI", "ui",
        "Materials", "materials", "Models", "models", "Prefabs", "Shaders", "shaders",
        "Audio", "audio", "Sounds", "sound", "Fonts", "font", "Skins", "skins",
        "Bundles", "bundle", "Content", "content", "Data", "data",
    };

    private static readonly string[] SkippedPathFragments =
    [
        $"{Path.DirectorySeparatorChar}.app{Path.DirectorySeparatorChar}",
        $"{Path.AltDirectorySeparatorChar}.app{Path.AltDirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Applications{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}SteamLibrary{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}steamapps{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Epic Games{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Program Files{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Program Files (x86){Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Windows{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}System32{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}SysWOW64{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Library{Path.DirectorySeparatorChar}Application Support{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Library{Path.DirectorySeparatorChar}Caches{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Library{Path.DirectorySeparatorChar}Intents{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}",
    ];

    public static bool ShouldSkipDirectory(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return true;

        absolutePath = Path.TrimEndingDirectorySeparator(absolutePath);

        if (ContainsSkippedFragment(absolutePath))
            return true;

        foreach (var segment in GetPathSegments(absolutePath))
        {
            if (SkippedDirectoryNames.Contains(segment))
                return true;

            if (segment.Contains("Trash", StringComparison.OrdinalIgnoreCase))
                return true;

            // Skip macOS app bundles and photos library packages (managed separately later).
            if (segment.EndsWith(".app", StringComparison.OrdinalIgnoreCase) ||
                segment.EndsWith(".photoslibrary", StringComparison.OrdinalIgnoreCase) ||
                segment.EndsWith(".framework", StringComparison.OrdinalIgnoreCase) ||
                segment.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool ShouldIncludePhotoFile(string absolutePath, long fileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return false;

        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory) && ShouldSkipDirectory(directory))
            return false;

        if (ContainsSkippedFragment(absolutePath))
            return false;

        var fileName = Path.GetFileName(absolutePath);
        if (string.IsNullOrEmpty(fileName))
            return false;

        // Skip obvious non-camera assets by name.
        if (fileName.StartsWith("icon", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("logo", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("sprite", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("texture", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("thumb", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("favicon", StringComparison.OrdinalIgnoreCase))
            return false;

        var ext = Path.GetExtension(absolutePath);
        return MeetsMinimumSize(ext, fileSizeBytes);
    }

    private static bool MeetsMinimumSize(string extension, long bytes)
    {
        if (bytes <= 0)
            return false;

        return extension.ToLowerInvariant() switch
        {
            ".png" or ".gif" or ".webp" or ".bmp" => bytes >= 25_000,
            ".jpg" or ".jpeg" or ".heic" or ".heif" or ".avif" => bytes >= 8_000,
            ".tif" or ".tiff" => bytes >= 50_000,
            _ => bytes >= 100_000, // RAW and other formats — likely real photos
        };
    }

    private static bool ContainsSkippedFragment(string path)
    {
        foreach (var fragment in SkippedPathFragments)
        {
            if (path.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> GetPathSegments(string absolutePath)
    {
        var parts = absolutePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
            yield return part;
    }
}
