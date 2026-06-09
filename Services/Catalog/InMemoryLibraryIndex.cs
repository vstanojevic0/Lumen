using Lumen.Core.Abstractions;
using Lumen.Core.Models;

namespace Lumen.Services.Catalog;

/// <summary>
/// In-memory read model populated from SQLite; refreshed after background sync.
/// </summary>
public sealed class InMemoryLibraryIndex : ILibraryIndex
{
    private readonly object _gate = new();
    private List<PhotoEntry> _photos = new();

    /// <summary>Absolute paths scanned (e.g. multiple drive roots on Windows).</summary>
    public List<string> ScanRoots { get; set; } = new();

    public async Task RebuildAsync(IAsyncEnumerable<PhotoEntry> source, CancellationToken cancellationToken = default)
    {
        var byPath = new Dictionary<string, PhotoEntry>(StringComparer.OrdinalIgnoreCase);
        await foreach (var p in source.WithCancellation(cancellationToken))
        {
            var key = Path.GetFullPath(p.AbsolutePath);
            byPath[key] = p with { AbsolutePath = key };
        }

        var list = byPath.Values.ToList();
        list.Sort(CompareByDateDescThenPath);

        lock (_gate)
            _photos = list;
    }

    public IReadOnlyList<FolderBrowseNode> GetFolderTree()
    {
        lock (_gate)
        {
            if (ScanRoots.Count == 0 || _photos.Count == 0)
                return Array.Empty<FolderBrowseNode>();

            var roots = ScanRoots
                .Select(Path.TrimEndingDirectorySeparator)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var directCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var photo in _photos)
            {
                var dir = Path.GetDirectoryName(photo.AbsolutePath);
                if (string.IsNullOrEmpty(dir))
                    continue;

                dir = Path.TrimEndingDirectorySeparator(dir);
                directCounts.TryGetValue(dir, out var n);
                directCounts[dir] = n + 1;
            }

            var nodes = new List<FolderBrowseNode>();
            foreach (var root in roots)
            {
                var label = FormatRootLabel(root);
                var trie = new TrieNode(root, label);

                foreach (var photo in _photos)
                {
                    var dir = Path.GetDirectoryName(photo.AbsolutePath);
                    if (string.IsNullOrEmpty(dir))
                        continue;

                    dir = Path.TrimEndingDirectorySeparator(dir);
                    if (!IsUnderRoot(dir, root))
                        continue;

                    var rel = Path.GetRelativePath(root, dir);
                    if (string.IsNullOrEmpty(rel) || rel == ".")
                        continue;

                    trie.InsertRelative(rel);
                }

                nodes.Add(trie.ToFolderBrowseNode(directCounts));
            }

            return nodes;
        }
    }

    private static string FormatRootLabel(string root)
    {
        root = Path.TrimEndingDirectorySeparator(root);
        if (OperatingSystem.IsWindows())
        {
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    var r = Path.TrimEndingDirectorySeparator(drive.RootDirectory.FullName);
                    if (!string.Equals(r, root, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var name = drive.Name.TrimEnd('\\', '/');
                    if (drive.IsReady && !string.IsNullOrWhiteSpace(drive.VolumeLabel))
                        return $"{drive.VolumeLabel} ({name})";
                    return $"Drive {name}";
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        var leaf = Path.GetFileName(root);
        return string.IsNullOrEmpty(leaf) ? root : leaf;
    }

    public IReadOnlyList<FolderHierarchyBucket> GetPhotosGroupedByFolder(string? folderPrefix = null)
    {
        lock (_gate)
            return BuildFolderBuckets(FilterByFolder(_photos, folderPrefix));
    }

    public IReadOnlyList<string> GetOrderedPhotoPaths(string? folderPrefix = null)
    {
        lock (_gate)
        {
            return FilterByFolder(_photos, folderPrefix)
                .Select(p => p.AbsolutePath)
                .ToList();
        }
    }

    private static List<FolderHierarchyBucket> BuildFolderBuckets(List<PhotoEntry> filtered)
    {
        return filtered
            .GroupBy(p => Path.GetDirectoryName(p.AbsolutePath) ?? string.Empty)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new FolderHierarchyBucket(
                Path.TrimEndingDirectorySeparator(g.Key),
                g.OrderBy(p => p.AbsolutePath, StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();
    }

    public IReadOnlyList<DateHierarchyBucket> GetPhotosGroupedByDate(
        string? folderPrefix,
        DateBucketGranularity granularity)
    {
        lock (_gate)
        {
            var filtered = FilterByFolder(_photos, folderPrefix);

            if (granularity == DateBucketGranularity.Month)
            {
                return filtered
                    .GroupBy(p => GetMonthKey(p.CapturedAt))
                    .OrderByDescending(g => g.Key ?? (0, 0))
                    .Select(g =>
                    {
                        if (g.Key is null)
                        {
                            return new DateHierarchyBucket(
                                DateBucketGranularity.Month,
                                0,
                                null,
                                null,
                                g.OrderBy(x => x.AbsolutePath, StringComparer.Ordinal).ToList());
                        }

                        var (year, month) = g.Key.Value;
                        return new DateHierarchyBucket(
                            DateBucketGranularity.Month,
                            year,
                            month,
                            null,
                            g.OrderByDescending(x => x.CapturedAt)
                                .ThenBy(x => x.AbsolutePath, StringComparer.Ordinal)
                                .ToList());
                    })
                    .ToList();
            }

            var dayGroups = filtered
                .GroupBy(p => GetDayKey(p.CapturedAt))
                .OrderByDescending(g => g.Key ?? DateOnly.MinValue)
                .Select(g =>
                {
                    var key = g.Key;
                    if (key is null)
                    {
                        return new DateHierarchyBucket(
                            DateBucketGranularity.Day,
                            0,
                            null,
                            null,
                            g.OrderBy(x => x.AbsolutePath, StringComparer.Ordinal).ToList());
                    }

                    return new DateHierarchyBucket(
                        DateBucketGranularity.Day,
                        key.Value.Year,
                        key.Value.Month,
                        key.Value.Day,
                        g.OrderByDescending(x => x.CapturedAt).ThenBy(x => x.AbsolutePath, StringComparer.Ordinal).ToList());
                })
                .ToList();

            return dayGroups;
        }
    }

    public PhotoEntry? GetByPath(string absolutePath)
    {
        lock (_gate)
            return _photos.FirstOrDefault(p => string.Equals(p.AbsolutePath, absolutePath, StringComparison.Ordinal));
    }

    public IReadOnlyList<PhotoEntry> GetInFolder(string absoluteFolderPath, bool recursive)
    {
        lock (_gate)
        {
            var folder = Path.TrimEndingDirectorySeparator(absoluteFolderPath);
            return _photos
                .Where(p =>
                {
                    var d = Path.GetDirectoryName(p.AbsolutePath);
                    if (string.IsNullOrEmpty(d))
                        return false;
                    d = Path.TrimEndingDirectorySeparator(d);
                    if (recursive)
                        return IsUnderRoot(d, folder) || string.Equals(d, folder, StringComparison.Ordinal);
                    return string.Equals(d, folder, StringComparison.Ordinal);
                })
                .OrderByDescending(p => p.CapturedAt)
                .ThenBy(p => p.AbsolutePath, StringComparer.Ordinal)
                .ToList();
        }
    }

    public int TotalPhotoCount()
    {
        lock (_gate)
            return _photos.Count;
    }

    private static List<PhotoEntry> FilterByFolder(IReadOnlyList<PhotoEntry> photos, string? folderPrefix)
    {
        if (string.IsNullOrEmpty(folderPrefix))
            return photos.ToList();

        var prefix = Path.TrimEndingDirectorySeparator(folderPrefix);
        return photos
            .Where(p =>
            {
                var d = Path.GetDirectoryName(p.AbsolutePath);
                if (string.IsNullOrEmpty(d))
                    return false;
                d = Path.TrimEndingDirectorySeparator(d);
                return string.Equals(d, prefix, StringComparison.Ordinal) || IsUnderRoot(d, prefix);
            })
            .ToList();
    }

    private static bool IsUnderRoot(string path, string root)
    {
        root = Path.TrimEndingDirectorySeparator(root);
        path = Path.TrimEndingDirectorySeparator(path);
        if (path.Length < root.Length)
            return false;
        if (!path.StartsWith(root, StringComparison.Ordinal))
            return false;
        return path.Length == root.Length || path[root.Length] == Path.DirectorySeparatorChar;
    }

    private static DateOnly? GetDayKey(DateTimeOffset? capturedAt)
    {
        if (capturedAt is null)
            return null;
        var l = capturedAt.Value.ToLocalTime();
        return DateOnly.FromDateTime(l.DateTime);
    }

    private static (int Year, int Month)? GetMonthKey(DateTimeOffset? capturedAt)
    {
        if (capturedAt is null)
            return null;
        var l = capturedAt.Value.ToLocalTime();
        return (l.Year, l.Month);
    }

    private static int CompareByDateDescThenPath(PhotoEntry a, PhotoEntry b)
    {
        var c = Nullable.Compare(b.CapturedAt, a.CapturedAt);
        if (c != 0)
            return c;
        return string.CompareOrdinal(a.AbsolutePath, b.AbsolutePath);
    }

    private sealed class TrieNode
    {
        public string FullPath { get; }
        public string SegmentName { get; }
        public Dictionary<string, TrieNode> Children { get; } = new(StringComparer.Ordinal);

        public TrieNode(string fullPath, string segmentName)
        {
            FullPath = fullPath;
            SegmentName = segmentName;
        }

        public void InsertRelative(string relativeDir)
        {
            var parts = relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var current = this;
            var built = FullPath;

            foreach (var part in parts)
            {
                if (!current.Children.TryGetValue(part, out var next))
                {
                    built = Path.Combine(built, part);
                    next = new TrieNode(built, part);
                    current.Children.Add(part, next);
                }

                current = next;
            }
        }

        public FolderBrowseNode ToFolderBrowseNode(IReadOnlyDictionary<string, int> directCounts)
        {
            var children = Children.Values
                .OrderBy(c => c.SegmentName, StringComparer.OrdinalIgnoreCase)
                .Select(c => c.ToFolderBrowseNode(directCounts))
                .ToList();

            directCounts.TryGetValue(FullPath, out var direct);
            var total = direct + children.Sum(c => c.PhotoCountTotal);

            return new FolderBrowseNode
            {
                AbsolutePath = FullPath,
                DisplayName = SegmentName,
                Children = children,
                PhotoCountDirect = direct,
                PhotoCountTotal = total
            };
        }
    }
}
