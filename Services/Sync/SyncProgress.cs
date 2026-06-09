namespace Lumen.Services.Sync;

public sealed class SyncProgress
{
    public string Phase { get; init; } = string.Empty;
    public int ProcessedFiles { get; init; }
    public int TotalFiles { get; init; }
    public int NewFiles { get; init; }
    public int UpdatedFiles { get; init; }
    public int MissingFiles { get; init; }
    public bool IsFullScan { get; init; }
}
