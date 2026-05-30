namespace Lumen.Services.Library;

/// <summary>
/// Suggested locations for the first-run wizard and default scan targets.
/// </summary>
public static class LibraryLocationCatalog
{
    public sealed record ScanCandidate(string AbsolutePath, string DisplayName);

    /// <summary>
    /// Windows: all ready fixed drives. macOS: Pictures only (personal photos, not entire Home).
    /// </summary>
    public static IReadOnlyList<ScanCandidate> GetSuggestedScanCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            var list = new List<ScanCandidate>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                        continue;

                    var root = Path.TrimEndingDirectorySeparator(drive.RootDirectory.FullName);
                    var label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                        ? $"Drive {drive.Name.TrimEnd('\\', '/')}"
                        : $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\', '/')})";
                    list.Add(new ScanCandidate(root, label));
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return list.OrderBy(c => c.AbsolutePath, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return GetMacOsCandidates();
    }

    private static IReadOnlyList<ScanCandidate> GetMacOsCandidates() => GetMacOsAutoLoadRoots();

    /// <summary>
    /// Default macOS library locations (Pictures, Desktop, Downloads) for startup auto-index.
    /// </summary>
    public static IReadOnlyList<ScanCandidate> GetMacOsAutoLoadRoots()
    {
        var list = new List<ScanCandidate>();
        AddIfExists(list, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Pictures");
        AddIfExists(list, Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Desktop");
        AddIfExists(list, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads",
            "Downloads");

        if (list.Count == 0)
            AddIfExists(list, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Home");

        return list;
    }

    private static void AddIfExists(List<ScanCandidate> list, string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        path = Path.TrimEndingDirectorySeparator(path);
        if (!Directory.Exists(path))
            return;

        list.Add(new ScanCandidate(path, label));
    }

    private static void AddIfExists(List<ScanCandidate> list, string parent, string subfolder, string label)
    {
        if (string.IsNullOrWhiteSpace(parent))
            return;

        AddIfExists(list, Path.Combine(parent, subfolder), label);
    }
}
