namespace Lumen.Core.Models;

/// <summary>
/// Grouping for year / month / day presentation in the central grid.
/// </summary>
public enum DateBucketGranularity
{
    Year,
    Month,
    Day
}

public sealed record DateHierarchyBucket(
    DateBucketGranularity Granularity,
    int Year,
    int? Month,
    int? Day,
    IReadOnlyList<PhotoEntry> Photos);
