namespace Lumen.Services.Database;

/// <summary>
/// Canonical path form for SQLite keys and comparisons (especially on Windows).
/// </summary>
public static class CatalogPathNormalizer
{
    public static string NormalizeFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return Path.GetFullPath(path.Trim());
    }

    public static string NormalizeFolderPath(string path)
    {
        path = NormalizeFilePath(path);
        return Path.TrimEndingDirectorySeparator(path);
    }

    public static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        return string.Equals(
            NormalizeFilePath(left),
            NormalizeFilePath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUnderRoot(string path, string root)
    {
        path = NormalizeFolderPath(path);
        root = NormalizeFolderPath(root);

        if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
            return true;

        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Removes scan roots that are nested inside another root (e.g. C:\Photos and C:\Photos\2024).
    /// Keeps the outermost path so each file is indexed once.
    /// </summary>
    public static List<string> PruneNestedScanRoots(IEnumerable<string> scanRoots)
    {
        var normalized = scanRoots
            .Select(NormalizeFolderPath)
            .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p.Length)
            .ToList();

        var kept = new List<string>();
        foreach (var root in normalized)
        {
            if (kept.Any(parent => IsUnderRoot(root, parent)))
                continue;

            kept.Add(root);
        }

        return kept;
    }
}
