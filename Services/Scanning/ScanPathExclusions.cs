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

        // Windows system & apps
        "Windows", "Program Files", "Program Files (x86)", "ProgramData", "AppData",
        "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs",
        "Microsoft", "Packages", "WinSxS", "Servicing", "WindowsApps", "SystemApps",
        "System32", "SysWOW64", "WinRE", "Boot", "servicing", "assembly", "Installer",
        "Prefetch", "SoftwareDistribution", "WUModels", "DigitalLocker", "GameBar",
        "INetCache", "InetCache", "History", "Thumbnails", "Thumbnail", "THUMBNAIL",
        "IconCache", "WebCache", "EBWebView", "GPUCache", "Code Cache", "Service Worker",
        "Extensions", "ShellExperiences", "SystemResources", "ImmersiveControlPanel",
        "TileDataLayer", "WinSAT", "Branding", "Cursors", "Help", "Package Cache",
        "PackageCache", "LocalLow", "Local Settings", "Temporary Internet Files",
        "DriverStore", "DriverStoreFileRepository", "FileRepository", "ASSET", "Asset",
        "Assets", "MediaAssets", "DeviceAssets", "ProductAssets", "CatalogAssets",
        "LGHUB", "lghub", "Logi", "Logitech", "LogiOptions", "LogiOptionsPlus",

        // Dev / package managers
        "node_modules", "bower_components", "vendor", "packages",
        "bin", "obj", "build", "dist", "out", "target", "gradle", ".gradle",

        // Game / engine asset trees
        "Steam", "steamapps", "Epic Games", "GOG Galaxy", "Unity", "UnrealEngine",
        "Unreal Engine", "Blender", "Godot", "RPG Maker", "World of Warcraft",
        "AssetBundles", "StreamingAssets", "Resources", "Art", "Artwork",
        "Textures", "textures", "Sprites", "sprites", "Icons", "icons", "UI", "ui",
        "Materials", "materials", "Models", "models", "Prefabs", "Shaders", "shaders",
        "Audio", "audio", "Sounds", "sound", "Fonts", "font", "Skins", "skins",
        "Bundles", "bundle", "Content", "content", "Data", "data",
    };

    private static readonly string[] SkippedPathFragments =
    [
        "$WINDOWS.~BT",
        "$WINDOWS.~WS",
        "$WinREAgent",
        $"{Path.DirectorySeparatorChar}.app{Path.DirectorySeparatorChar}",
        $"{Path.AltDirectorySeparatorChar}.app{Path.AltDirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Applications{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}SteamLibrary{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}steamapps{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Epic Games{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Program Files{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Program Files (x86){Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}ProgramData{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}AppData{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Windows{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}WindowsApps{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}SystemApps{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}System32{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}SysWOW64{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}WinSxS{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Installer{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Package Cache{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}PackageCache{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}INetCache{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Thumbnails{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Thumbnail{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}THUMBNAIL{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}ASSET{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Assets{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}DriverStore{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}DriverStoreFileRepository{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}FileRepository{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}IconCache{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}LGHUB{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}lghub{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}LogiOptions{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}LogiOptionsPlus{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Logitech{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Microsoft{Path.DirectorySeparatorChar}Windows{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Microsoft{Path.DirectorySeparatorChar}Edge{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Google{Path.DirectorySeparatorChar}Chrome{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Mozilla{Path.DirectorySeparatorChar}Firefox{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Library{Path.DirectorySeparatorChar}Application Support{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Library{Path.DirectorySeparatorChar}Caches{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Library{Path.DirectorySeparatorChar}Intents{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Sources{Path.DirectorySeparatorChar}Diagnostics{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Sources{Path.DirectorySeparatorChar}Replacement{Path.DirectorySeparatorChar}",
    ];

    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".orf", ".raf", ".rw2", ".dng", ".pef", ".srw", ".3fr"
    };

    public static bool ShouldSkipDirectory(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return true;

        absolutePath = Path.TrimEndingDirectorySeparator(absolutePath);

        if (IsWindowsSystemVolumePath(absolutePath))
            return true;

        if (ContainsSkippedFragment(absolutePath))
            return true;

        foreach (var segment in GetPathSegments(absolutePath))
        {
            if (IsWindowsSystemSegment(segment))
                return true;

            if (SkippedDirectoryNames.Contains(segment))
                return true;

            if (segment.Contains("Trash", StringComparison.OrdinalIgnoreCase))
                return true;

            if (segment.EndsWith(".app", StringComparison.OrdinalIgnoreCase) ||
                segment.EndsWith(".photoslibrary", StringComparison.OrdinalIgnoreCase) ||
                segment.EndsWith(".framework", StringComparison.OrdinalIgnoreCase) ||
                segment.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool ShouldIncludeCatalogPhoto(
        string absolutePath,
        string extension,
        long fileSizeBytes,
        int? width,
        int? height)
    {
        if (!ShouldIncludePhotoFile(absolutePath, fileSizeBytes))
            return false;

        if (!ShouldIncludePhotoDimensions(extension, width, height))
            return false;

        if (LooksLikeUiProductAsset(absolutePath, extension, width, height))
            return false;

        return true;
    }

    public static bool ShouldIncludePhotoFile(string absolutePath, long fileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return false;

        if (IsWindowsSystemVolumePath(absolutePath))
            return false;

        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory) && ShouldSkipDirectory(directory))
            return false;

        if (ContainsSkippedFragment(absolutePath))
            return false;

        var fileName = Path.GetFileName(absolutePath);
        if (string.IsNullOrEmpty(fileName))
            return false;

        if (LooksLikeAssetFileName(fileName))
            return false;

        var ext = Path.GetExtension(absolutePath);
        if (IsWindowsAssetPath(absolutePath) && IsRasterAssetExtension(ext))
            return false;

        return MeetsMinimumSize(ext, fileSizeBytes);
    }

    public static bool ShouldIncludePhotoDimensions(string extension, int? width, int? height)
    {
        if (width is null or <= 0 || height is null or <= 0)
            return true;

        if (RawExtensions.Contains(extension))
            return true;

        var longestEdge = Math.Max(width.Value, height.Value);
        var shortestEdge = Math.Min(width.Value, height.Value);

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".heic" or ".heif" or ".avif" =>
                longestEdge >= 480 && shortestEdge >= 320,
            ".png" or ".gif" or ".webp" or ".bmp" =>
                longestEdge >= 640 && shortestEdge >= 480,
            ".tif" or ".tiff" =>
                longestEdge >= 800,
            _ => longestEdge >= 480,
        };
    }

    private static bool IsWindowsSystemVolumePath(string path) =>
        path.Contains("$WINDOWS.~BT", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("$WINDOWS.~WS", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("$WinREAgent", StringComparison.OrdinalIgnoreCase);

    private static bool IsWindowsSystemSegment(string segment) =>
        segment.StartsWith('$') ||
        segment.StartsWith("Windows10Upgrade", StringComparison.OrdinalIgnoreCase) ||
        segment.StartsWith("Windows11Upgrade", StringComparison.OrdinalIgnoreCase);

    private static bool IsWindowsAssetPath(string path) =>
        IsWindowsSystemVolumePath(path) ||
        path.Contains("\\ASSET\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\Assets\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\THUMBNAIL\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\Thumbnail\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\DriverStore\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\LGHUB\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\lghub\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\LogiOptions", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\Logitech\\", StringComparison.OrdinalIgnoreCase);

    private static bool IsRasterAssetExtension(string extension) =>
        extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeUiProductAsset(string path, string extension, int? width, int? height)
    {
        if (!IsRasterAssetExtension(extension) || width is null or <= 0 || height is null or <= 0)
            return false;

        if (IsWindowsAssetPath(path))
            return true;

        var longest = Math.Max(width.Value, height.Value);
        var shortest = Math.Min(width.Value, height.Value);
        var squareish = shortest / (double)longest >= 0.82;

        if (!squareish || longest > 1400)
            return false;

        return path.Contains("\\Program Files\\", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("\\Program Files (x86)\\", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("\\Windows\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeAssetFileName(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();

        if (stem.StartsWith("icon", StringComparison.Ordinal) ||
            stem.StartsWith("logo", StringComparison.Ordinal) ||
            stem.StartsWith("sprite", StringComparison.Ordinal) ||
            stem.StartsWith("texture", StringComparison.Ordinal) ||
            stem.StartsWith("thumb", StringComparison.Ordinal) ||
            stem.StartsWith("tile", StringComparison.Ordinal) ||
            stem.StartsWith("badge", StringComparison.Ordinal) ||
            stem.StartsWith("banner", StringComparison.Ordinal) ||
            stem.StartsWith("splash", StringComparison.Ordinal) ||
            stem.StartsWith("placeholder", StringComparison.Ordinal) ||
            stem.StartsWith("appx", StringComparison.Ordinal) ||
            stem.StartsWith("msix", StringComparison.Ordinal) ||
            stem.StartsWith("device_", StringComparison.Ordinal) ||
            stem.StartsWith("product_", StringComparison.Ordinal))
            return true;

        if (lower.Contains("favicon") ||
            lower.Contains("@2x") ||
            lower.Contains("@3x") ||
            lower.Contains("appicon") ||
            lower.Contains("storelogo") ||
            lower.Contains("square150x150") ||
            lower.Contains("wide310x150") ||
            lower.Contains("splashscreen") ||
            lower.Contains("windows.ui") ||
            lower.Contains("microsoft.windows") ||
            lower.Contains("report_rate") ||
            lower.Contains("battery") ||
            lower.Contains("bluetooth") ||
            lower.Contains("wifi") ||
            lower.Contains("notification") ||
            lower.Contains("volume_") ||
            lower.Contains("brightness") ||
            lower.Contains("airplane") ||
            lower.Contains("hotspot"))
            return true;

        if (stem.EndsWith("_16", StringComparison.Ordinal) ||
            stem.EndsWith("_32", StringComparison.Ordinal) ||
            stem.EndsWith("_48", StringComparison.Ordinal) ||
            stem.EndsWith("_64", StringComparison.Ordinal) ||
            stem.EndsWith("_128", StringComparison.Ordinal) ||
            stem.EndsWith("_256", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static bool MeetsMinimumSize(string extension, long bytes)
    {
        if (bytes <= 0)
            return false;

        return extension.ToLowerInvariant() switch
        {
            ".png" or ".gif" or ".webp" or ".bmp" => bytes >= 80_000,
            ".jpg" or ".jpeg" or ".heic" or ".heif" or ".avif" => bytes >= 40_000,
            ".tif" or ".tiff" => bytes >= 120_000,
            _ => bytes >= 150_000,
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
