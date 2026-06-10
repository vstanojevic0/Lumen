namespace Lumen.Core.Models;

public sealed class ScanStateRecord
{
    public long Id { get; init; } = 1;
    public DateTimeOffset? LastFullScanAt { get; init; }
    public DateTimeOffset? LastIncrementalScanAt { get; init; }
    public string? AppVersion { get; init; }
    public int CatalogRepairVersion { get; init; }
    public string? LastSyncError { get; init; }
}
