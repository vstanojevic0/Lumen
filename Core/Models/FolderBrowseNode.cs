namespace Lumen.Core.Models;

/// <summary>
/// A node in the folder tree (left panel). Children can be loaded lazily later.
/// </summary>
public sealed class FolderBrowseNode
{
    public required string AbsolutePath { get; init; }
    public required string DisplayName { get; init; }
    public IReadOnlyList<FolderBrowseNode> Children { get; init; } = Array.Empty<FolderBrowseNode>();
    public int PhotoCountDirect { get; init; }
    public int PhotoCountTotal { get; init; }
}
