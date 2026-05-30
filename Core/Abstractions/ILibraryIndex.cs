using Lumen.Core.Models;

namespace Lumen.Core.Abstractions;

/// <summary>
/// Fast queries over the catalog (later: SQLite/FTS or LiteDB). Used by the folder tree, date grid, and path filters.
/// </summary>
public interface ILibraryIndex
{
    Task RebuildAsync(IAsyncEnumerable<PhotoEntry> source, CancellationToken cancellationToken = default);

    IReadOnlyList<FolderBrowseNode> GetFolderTree();

    IReadOnlyList<DateHierarchyBucket> GetPhotosGroupedByDate(
        string? folderPrefix,
        DateBucketGranularity granularity);

    IReadOnlyList<FolderHierarchyBucket> GetPhotosGroupedByFolder(string? folderPrefix = null);

    PhotoEntry? GetByPath(string absolutePath);

    IReadOnlyList<PhotoEntry> GetInFolder(string absoluteFolderPath, bool recursive);
}
