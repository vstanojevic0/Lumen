namespace Lumen.Core.Models;

/// <summary>
/// Photos grouped by containing directory for the unified folder timeline.
/// </summary>
public sealed record FolderHierarchyBucket(
    string FolderPath,
    IReadOnlyList<PhotoEntry> Photos);
