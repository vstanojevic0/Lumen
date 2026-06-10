using Lumen.Core.Models;
using Lumen.Services.Cache;
using Lumen.Services.Catalog;
using Lumen.Services.Database;
using Lumen.Services.Metadata;
using Lumen.Services.Scanning;

namespace Lumen.Services.Sync;

/// <summary>
/// Loads the catalog from SQLite and performs incremental background sync with disk.
/// </summary>
public sealed class LibrarySyncService
{
    public const double MinimumCatalogCoverageRatio = 0.85;

    private const int DbBatchSize = 200;
    private const int IndexReloadInterval = 5000;

    private readonly LocalDatabaseService _database;
    private readonly FolderRepository _folders;
    private readonly PhotoRepository _photos;
    private readonly ScanStateRepository _scanState;
    private readonly PhotoScannerService _scanner;
    private readonly MetadataExtractorService _metadata;
    private readonly ThumbnailBacklogService _thumbnailBacklog;

    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public LibrarySyncService(
        LocalDatabaseService database,
        FolderRepository folders,
        PhotoRepository photos,
        ScanStateRepository scanState,
        PhotoScannerService scanner,
        MetadataExtractorService metadata,
        ThumbnailBacklogService thumbnailBacklog)
    {
        _database = database;
        _folders = folders;
        _photos = photos;
        _scanState = scanState;
        _scanner = scanner;
        _metadata = metadata;
        _thumbnailBacklog = thumbnailBacklog;
    }

    public IReadOnlyList<PhotoEntry> LoadVisiblePhotos(IReadOnlyList<string> scanRoots)
    {
        _database.EnsureInitialized();
        _folders.SyncScanRoots(scanRoots);
        return _photos.GetAllVisible().Select(p => p.ToPhotoEntry()).ToList();
    }

    public async Task ReloadIndexAsync(
        InMemoryLibraryIndex index,
        IReadOnlyList<string> scanRoots,
        CancellationToken cancellationToken = default)
    {
        index.ScanRoots = CatalogPathNormalizer.PruneNestedScanRoots(scanRoots);

        var entries = await Task.Run(() => LoadVisiblePhotos(index.ScanRoots), cancellationToken).ConfigureAwait(false);
        await index.RebuildAsync(ToAsyncEnumerable(entries), cancellationToken).ConfigureAwait(false);
    }

    public async Task<SyncSummary> RunIncrementalSyncAsync(
        InMemoryLibraryIndex index,
        IReadOnlyList<string> scanRoots,
        bool fullRescan,
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!fullRescan && !await _syncGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return SyncSummary.Skipped();

        if (fullRescan)
            await _syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await RunIncrementalSyncCoreAsync(index, scanRoots, fullRescan, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task<SyncSummary> RunIncrementalSyncCoreAsync(
        InMemoryLibraryIndex index,
        IReadOnlyList<string> scanRoots,
        bool fullRescan,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        _database.EnsureInitialized();
        _folders.SyncScanRoots(scanRoots);

        var roots = CatalogPathNormalizer.PruneNestedScanRoots(scanRoots)
            .Where(Directory.Exists)
            .ToList();

        if (roots.Count == 0)
        {
            index.ScanRoots = roots;
            await index.RebuildAsync(ToAsyncEnumerable(Array.Empty<PhotoEntry>()), cancellationToken)
                .ConfigureAwait(false);
            return new SyncSummary(0, 0, 0, 0, true);
        }

        var summary = new SyncAccumulator();
        Report(progress, "Scanning folders…", summary, fullRescan);

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var folderId = _folders.UpsertFolder(root, Path.GetFileName(root) ?? root);
                await SyncFolderAsync(
                        index,
                        roots,
                        folderId,
                        root,
                        summary,
                        progress,
                        fullRescan,
                        cancellationToken)
                    .ConfigureAwait(false);
                _folders.SetLastScannedAt(folderId, DateTimeOffset.UtcNow);
                await ReloadIndexAsync(index, roots, cancellationToken).ConfigureAwait(false);
                Report(progress, $"Updated {root}", summary, fullRescan);
            }
            catch (Exception ex) when (FileSystemPhotoScanner.IsBenignAccessFailure(ex))
            {
                Report(progress, $"Skipped {root}", summary, fullRescan);
            }
        }

        await ReloadIndexAsync(index, roots, cancellationToken).ConfigureAwait(false);

        var dbCount = _photos.CountVisible();
        var diskCount = await Task.Run(() => CountPhotosOnDisk(roots, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
        var complete = diskCount == 0 || dbCount >= diskCount * MinimumCatalogCoverageRatio;

        var now = DateTimeOffset.UtcNow;
        var version = typeof(LibrarySyncService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        if (complete)
        {
            if (fullRescan)
                _scanState.SetFullScanAt(now, version);
            else
                _scanState.SetIncrementalScanAt(now, version);
        }
        else
        {
            _scanState.SetLastSyncError(
                $"Indexed {dbCount:N0} of ~{diskCount:N0} photos — scan will continue automatically.");
        }

        Report(progress, complete ? "Sync complete" : "Continuing index…", summary, fullRescan);
        return summary.ToSummary(complete, dbCount, diskCount);
    }

    private async Task SyncFolderAsync(
        InMemoryLibraryIndex index,
        IReadOnlyList<string> scanRoots,
        long folderId,
        string root,
        SyncAccumulator summary,
        IProgress<SyncProgress>? progress,
        bool fullRescan,
        CancellationToken cancellationToken)
    {
        var existing = await Task.Run(() => _photos.GetByFolderId(folderId), cancellationToken)
            .ConfigureAwait(false);

        var seenOnDisk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var newRecords = new List<NewPhotoRecord>();
        var updates = new List<PhotoUpdateRecord>();
        var excludedIds = new List<long>();
        var filesSinceReload = 0;

        foreach (var rawPath in _scanner.EnumeratePhotoPaths(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = CatalogPathNormalizer.NormalizeFilePath(rawPath);
            if (!seenOnDisk.Add(path))
                continue;

            summary.TotalFiles++;
            summary.ProcessedFiles++;
            filesSinceReload++;

            if (summary.ProcessedFiles % 500 == 0)
                Report(progress, $"Checking {root}…", summary, fullRescan);

            if (!existing.TryGetValue(path, out var record))
            {
                record = await Task.Run(() => _photos.GetByPath(path), cancellationToken)
                    .ConfigureAwait(false);
                if (record is not null)
                    existing[path] = record;
            }

            if (record is null)
            {
                var extracted = await Task.Run(() => _metadata.TryExtract(path, includeDimensions: false), cancellationToken)
                    .ConfigureAwait(false);
                if (extracted is null)
                    continue;

                var now = DateTimeOffset.UtcNow;
                newRecords.Add(new NewPhotoRecord
                {
                    FolderId = folderId,
                    FilePath = extracted.FilePath,
                    FileName = extracted.FileName,
                    Extension = extracted.Extension,
                    FileSize = extracted.FileSize,
                    DateCreated = extracted.DateCreated,
                    DateModified = extracted.DateModified,
                    DateTaken = extracted.DateTaken,
                    Width = extracted.Width,
                    Height = extracted.Height,
                    Hash = extracted.Hash,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                summary.NewFiles++;

                if (newRecords.Count >= DbBatchSize)
                {
                    await FlushNewRecordsAsync(newRecords, cancellationToken).ConfigureAwait(false);
                    newRecords.Clear();
                }

                continue;
            }

            if (record.IsMissing || fullRescan || NeedsUpdate(record, path))
            {
                var extracted = await Task.Run(() => _metadata.TryExtract(path, includeDimensions: false), cancellationToken)
                    .ConfigureAwait(false);
                if (extracted is null)
                {
                    if (!record.IsMissing)
                        excludedIds.Add(record.Id);
                    continue;
                }

                var reuseThumbs = !record.IsMissing &&
                                  !fullRescan &&
                                  string.Equals(record.Hash, extracted.Hash, StringComparison.Ordinal) &&
                                  !string.IsNullOrWhiteSpace(record.ThumbnailPath);

                updates.Add(new PhotoUpdateRecord
                {
                    Id = record.Id,
                    FileSize = extracted.FileSize,
                    DateCreated = extracted.DateCreated,
                    DateModified = extracted.DateModified,
                    DateTaken = extracted.DateTaken,
                    Width = extracted.Width ?? record.Width,
                    Height = extracted.Height ?? record.Height,
                    Hash = extracted.Hash,
                    ThumbnailPath = reuseThumbs ? record.ThumbnailPath : null,
                    ThumbnailMediumPath = reuseThumbs ? record.ThumbnailMediumPath : null,
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                if (!reuseThumbs)
                    _thumbnailBacklog.Enqueue(record.Id, path, forceRegenerate: true);

                summary.UpdatedFiles++;

                if (updates.Count >= DbBatchSize)
                {
                    await FlushUpdatesAsync(updates, cancellationToken).ConfigureAwait(false);
                    updates.Clear();
                }
            }

            if (filesSinceReload >= IndexReloadInterval)
            {
                if (newRecords.Count > 0)
                {
                    await FlushNewRecordsAsync(newRecords, cancellationToken).ConfigureAwait(false);
                    newRecords.Clear();
                }

                if (updates.Count > 0)
                {
                    await FlushUpdatesAsync(updates, cancellationToken).ConfigureAwait(false);
                    updates.Clear();
                }

                await ReloadIndexAsync(index, scanRoots, cancellationToken).ConfigureAwait(false);
                filesSinceReload = 0;
            }
        }

        if (newRecords.Count > 0)
            await FlushNewRecordsAsync(newRecords, cancellationToken).ConfigureAwait(false);

        if (updates.Count > 0)
            await FlushUpdatesAsync(updates, cancellationToken).ConfigureAwait(false);

        if (excludedIds.Count > 0)
        {
            await Task.Run(() => _photos.MarkMissingBatch(excludedIds), cancellationToken).ConfigureAwait(false);
            summary.MissingFiles += excludedIds.Count;
        }

        var missingIds = existing.Values
            .Where(p => !p.IsMissing && !seenOnDisk.Contains(CatalogPathNormalizer.NormalizeFilePath(p.FilePath)))
            .Select(p => p.Id)
            .ToList();

        if (missingIds.Count > 0)
        {
            await Task.Run(() => _photos.MarkMissingBatch(missingIds), cancellationToken).ConfigureAwait(false);
            summary.MissingFiles += missingIds.Count;
        }
    }

    private async Task FlushNewRecordsAsync(
        List<NewPhotoRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
            return;

        var batch = records.ToList();
        await Task.Run(() => _photos.InsertPhotosBatch(batch), cancellationToken).ConfigureAwait(false);

        foreach (var record in batch)
        {
            var saved = await Task.Run(() => _photos.GetByPath(record.FilePath), cancellationToken)
                .ConfigureAwait(false);
            if (saved is null)
                continue;

            _thumbnailBacklog.Enqueue(saved.Id, saved.FilePath, forceRegenerate: true);
        }
    }

    private async Task FlushUpdatesAsync(
        List<PhotoUpdateRecord> updates,
        CancellationToken cancellationToken)
    {
        if (updates.Count == 0)
            return;

        var batch = updates.ToList();
        await Task.Run(() => _photos.UpdatePhotosBatch(batch), cancellationToken).ConfigureAwait(false);
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

    private static bool NeedsUpdate(CatalogPhotoRecord record, string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return false;

            if (record.FileSize != info.Length)
                return true;

            var modified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
            return Math.Abs((modified - record.DateModified).TotalSeconds) > 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Report(IProgress<SyncProgress>? progress, string phase, SyncAccumulator summary, bool fullRescan)
    {
        progress?.Report(new SyncProgress
        {
            Phase = phase,
            ProcessedFiles = summary.ProcessedFiles,
            TotalFiles = summary.TotalFiles,
            NewFiles = summary.NewFiles,
            UpdatedFiles = summary.UpdatedFiles,
            MissingFiles = summary.MissingFiles,
            IsFullScan = fullRescan
        });
    }

    private static async IAsyncEnumerable<PhotoEntry> ToAsyncEnumerable(IEnumerable<PhotoEntry> source)
    {
        foreach (var entry in source)
            yield return entry;
        await Task.CompletedTask;
    }

    private sealed class SyncAccumulator
    {
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public int NewFiles { get; set; }
        public int UpdatedFiles { get; set; }
        public int MissingFiles { get; set; }

        public SyncSummary ToSummary(bool complete, int indexedCount, int diskCount) =>
            new(NewFiles, UpdatedFiles, MissingFiles, ProcessedFiles, complete, indexedCount, diskCount);
    }
}

public sealed record SyncSummary(
    int NewFiles,
    int UpdatedFiles,
    int MissingFiles,
    int ProcessedFiles,
    bool IsComplete = true,
    int IndexedCount = 0,
    int DiskCount = 0)
{
    public static SyncSummary Skipped() => new(0, 0, 0, 0, false);
}
