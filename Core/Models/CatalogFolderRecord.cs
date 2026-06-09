namespace Lumen.Core.Models;

public sealed class CatalogFolderRecord
{
    public long Id { get; init; }
    public string Path { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DateTimeOffset? LastScannedAt { get; init; }
    public bool IsActive { get; init; } = true;
}
