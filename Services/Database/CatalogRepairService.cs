using Lumen.Core.Models;
using Lumen.Services.Scanning;

namespace Lumen.Services.Database;

/// <summary>
/// Repairs the local catalog and decides when a full rescan is needed — no manual DB cleanup.
/// </summary>
public sealed class CatalogRepairService
{
    public const int CurrentRepairVersion = 1;

    /// <summary>Indexed count must reach this fraction of files on disk before we consider the catalog complete.</summary>
    private const double MinimumCatalogCoverageRatio = 0.85;

    private readonly LocalDatabaseService _database;
    private readonly PhotoRepository _photos;
    private readonly ScanStateRepository _scanState;
    private readonly PhotoScannerService _scanner;

    public CatalogRepairService(
        LocalDatabaseService database,
        PhotoRepository photos,
        ScanStateRepository scanState,
        PhotoScannerService scanner)
    {
        _database = database;
        _photos = photos;
        _scanState = scanState;
        _scanner = scanner;
    }

    public CatalogSyncPlan AssessSyncPlan(IReadOnlyList<string> scanRoots, CancellationToken cancellationToken = default)
    {
        _database.EnsureInitialized();
        var roots = CatalogPathNormalizer.PruneNestedScanRoots(scanRoots);
        var plan = new CatalogSyncPlan();
        var repair = RunStructuralRepair();
        plan.RepairMadeChanges = repair.Changed;

        var dbCount = _photos.CountVisible();
        var state = _scanState.Get();

        if (roots.Count == 0)
            return plan;

        if (dbCount == 0)
        {
            plan.NeedsFullRescan = true;
            plan.Reason = "empty catalog";
            return plan;
        }

        if (!string.IsNullOrWhiteSpace(state.LastSyncError))
        {
            plan.NeedsFullRescan = true;
            plan.Reason = "recovering from sync error";
            return plan;
        }

        if (repair.Changed)
        {
            plan.NeedsFullRescan = true;
            plan.Reason = "catalog repaired";
            return plan;
        }

        if (ShouldVerifyDiskCoverage(state, dbCount))
        {
            var diskCount = CountPhotosOnDisk(roots, cancellationToken);
            if (diskCount > 0 && dbCount < diskCount * MinimumCatalogCoverageRatio)
            {
                plan.NeedsFullRescan = true;
                plan.Reason = "finishing library index";
                return plan;
            }
        }

        return plan;
    }

    public CatalogRepairSummary RepairCatalog()
    {
        _database.EnsureInitialized();
        return RunStructuralRepair();
    }

    private CatalogRepairSummary RunStructuralRepair()
    {
        var summary = new CatalogRepairSummary();
        var state = _scanState.Get();

        if (state.CatalogRepairVersion >= CurrentRepairVersion)
            return summary;

        summary.PathsNormalized = NormalizeStoredPaths();
        summary.DuplicatesRemoved = RemoveDuplicatePaths();
        _scanState.SetCatalogRepairVersion(CurrentRepairVersion);
        return summary;
    }

    private static bool ShouldVerifyDiskCoverage(ScanStateRecord state, int dbCount)
    {
        if (!string.IsNullOrWhiteSpace(state.LastSyncError))
            return true;

        if (state.LastFullScanAt is null)
            return true;

        if (dbCount < 250)
            return true;

        if (Version.TryParse(state.AppVersion, out var version) && version < new Version(0, 3, 4))
            return true;

        return false;
    }

    private int CountPhotosOnDisk(IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var root in roots)
        {
            foreach (var _ in _scanner.EnumeratePhotoPaths(root, cancellationToken))
            {
                count++;
                if (count % 5000 == 0)
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return count;
    }

    private int NormalizeStoredPaths()
    {
        using var connection = _database.OpenConnection();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT Id, FilePath FROM Photos;";

        var updates = new List<(long Id, string Path)>();
        using (var reader = select.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var current = reader.GetString(1);
                var normalized = CatalogPathNormalizer.NormalizeFilePath(current);
                if (!CatalogPathNormalizer.PathsEqual(current, normalized))
                    updates.Add((id, normalized));
            }
        }

        if (updates.Count == 0)
            return 0;

        using var transaction = connection.BeginTransaction();
        foreach (var (id, path) in updates)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE Photos SET FilePath = $path WHERE Id = $id;";
            update.Parameters.AddWithValue("$path", path);
            update.Parameters.AddWithValue("$id", id);
            update.ExecuteNonQuery();
        }

        transaction.Commit();
        return updates.Count;
    }

    private int RemoveDuplicatePaths()
    {
        using var connection = _database.OpenConnection();
        using var select = connection.CreateCommand();
        select.CommandText = """
            SELECT FilePath
            FROM Photos
            GROUP BY FilePath COLLATE NOCASE
            HAVING COUNT(*) > 1;
            """;

        var duplicatePaths = new List<string>();
        using (var reader = select.ExecuteReader())
        {
            while (reader.Read())
                duplicatePaths.Add(reader.GetString(0));
        }

        if (duplicatePaths.Count == 0)
            return 0;

        var removed = 0;
        using var transaction = connection.BeginTransaction();

        foreach (var path in duplicatePaths)
        {
            using var rows = connection.CreateCommand();
            rows.Transaction = transaction;
            rows.CommandText = """
                SELECT Id, ThumbnailPath, ThumbnailMediumPath, UpdatedAt
                FROM Photos
                WHERE FilePath = $path COLLATE NOCASE
                ORDER BY
                    CASE WHEN ThumbnailPath IS NOT NULL AND ThumbnailPath != '' THEN 0 ELSE 1 END,
                    UpdatedAt DESC,
                    Id DESC;
                """;
            rows.Parameters.AddWithValue("$path", path);

            var ids = new List<long>();
            using (var reader = rows.ExecuteReader())
            {
                while (reader.Read())
                    ids.Add(reader.GetInt64(0));
            }

            foreach (var id in ids.Skip(1))
            {
                using var delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM Photos WHERE Id = $id;";
                delete.Parameters.AddWithValue("$id", id);
                delete.ExecuteNonQuery();
                removed++;
            }
        }

        transaction.Commit();
        return removed;
    }
}

public sealed class CatalogSyncPlan
{
    public bool NeedsFullRescan { get; set; }
    public bool RepairMadeChanges { get; set; }
    public string? Reason { get; set; }
}

public sealed class CatalogRepairSummary
{
    public int PathsNormalized { get; set; }
    public int DuplicatesRemoved { get; set; }
    public bool Changed => PathsNormalized > 0 || DuplicatesRemoved > 0;
}
