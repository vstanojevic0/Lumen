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
    private const int DbBatchSize = 200;
    private const int ThumbnailBatchSize = 8;

    private readonly LocalDatabaseService _database;
    private readonly FolderRepository _folders;
    private readonly PhotoRepository _photos;
    private readonly ScanStateRepository _scanState;
    private readonly PhotoScannerService _scanner;
    private readonly MetadataExtractorService _metadata;
    private readonly ThumbnailCacheService _thumbnails;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public LibrarySyncService(
        LocalDatabaseService database,
        FolderRepository folders,
        PhotoRepository photos,
        ScanStateRepository scanState,
        PhotoScannerService scanner,
        MetadataExtractorService metadata,
        ThumbnailCacheService thumbnails)
    {
        _database = database;
        _folders = folders;
        _photos = photos;
        _scanState = scanState;
        _scanner = scanner;
        _metadata = metadata;
        _thumbnails = thumbnails;
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
        index.ScanRoots = scanRoots
            .Select(Path.TrimEndingDirectorySeparator)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entries = await Task.Run(() => LoadVisiblePhotos(scanRoots), cancellationToken).ConfigureAwait(false);
        await index.RebuildAsync(ToAsyncEnumerable(entries), cancellationToken).ConfigureAwait(false);
    }

    public async Task<SyncSummary> RunIncrementalSyncAsync(
        InMemoryLibraryIndex index,
        IReadOnlyList<string> scanRoots,
        bool fullRescan,
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!await _syncGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return SyncSummary.Skipped();

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

        var roots = scanRoots
            .Select(Path.TrimEndingDirectorySeparator)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roots.Count == 0)
        {
            index.ScanRoots = roots;
            await index.RebuildAsync(ToAsyncEnumerable(Array.Empty<PhotoEntry>()), cancellationToken)
                .ConfigureAwait(false);
            return new SyncSummary(0, 0, 0, 0);
        }

        var summary = new SyncAccumulator();
        Report(progress, "Scanning folders…", summary, fullRescan);

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderId = _folders.UpsertFolder(root, Path.GetFileName(root) ?? root);
            await SyncFolderAsync(folderId, root, summary, progress, fullRescan, cancellationToken)
                .ConfigureAwait(false);
            _folders.SetLastScannedAt(folderId, DateTimeOffset.UtcNow);
            await ReloadIndexAsync(index, roots, cancellationToken).ConfigureAwait(false);
            Report(progress, $"Updated {root}", summary, fullRescan);
        }

        var now = DateTimeOffset.UtcNow;
        var version = typeof(LibrarySyncService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        if (fullRescan)
            _scanState.SetFullScanAt(now, version);
        else
            _scanState.SetIncrementalScanAt(now, version);

        Report(progress, "Sync complete", summary, fullRescan);
        return summary.ToSummary();
    }

    private async Task SyncFolderAsync(
        long folderId,
        string root,
        SyncAccumulator summary,
        IProgress<SyncProgress>? progress,
        bool fullRescan,
        CancellationToken cancellationToken)
    {
        var existing = await Task.Run(() => _photos.GetByFolderId(folderId), cancellationToken)
            .ConfigureAwait(false);

        var diskPaths = await Task.Run(
            () => _scanner.EnumeratePhotoPaths(root, cancellationToken).ToHashSet(StringComparer.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);

        summary.TotalFiles += diskPaths.Count;
        var newRecords = new List<NewPhotoRecord>();
        var updates = new List<PhotoUpdateRecord>();
        var thumbnailJobs = new List<ThumbnailJob>();

        foreach (var path in diskPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            summary.ProcessedFiles++;

            if (summary.ProcessedFiles % 500 == 0)
                Report(progress, $"Checking {root}…", summary, fullRescan);

            if (!existing.TryGetValue(path, out var record))
            {
                var extracted = await Task.Run(() => _metadata.TryExtract(path), cancellationToken)
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
                    await FlushNewRecordsAsync(newRecords, thumbnailJobs, cancellationToken).ConfigureAwait(false);
                    newRecords.Clear();
                }

                continue;
            }

            if (record.IsMissing || fullRescan || NeedsUpdate(record, path))
            {
                var extracted = await Task.Run(() => _metadata.TryExtract(path), cancellationToken)
                    .ConfigureAwait(false);
                if (extracted is null)
                    continue;

                var reuseThumbs = !record.IsMissing &&
                                  !fullRescan &&
                                  string.Equals(record.Hash, extracted.Hash, StringComparison.Ordinal) &&
                                  _thumbnails.HasValidThumbnails(record.Id, record.ThumbnailPath, record.ThumbnailMediumPath);

                updates.Add(new PhotoUpdateRecord
                {
                    Id = record.Id,
                    FileSize = extracted.FileSize,
                    DateCreated = extracted.DateCreated,
                    DateModified = extracted.DateModified,
                    DateTaken = extracted.DateTaken,
                    Width = extracted.Width,
                    Height = extracted.Height,
                    Hash = extracted.Hash,
                    ThumbnailPath = reuseThumbs ? record.ThumbnailPath : null,
                    ThumbnailMediumPath = reuseThumbs ? record.ThumbnailMediumPath : null,
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                if (!reuseThumbs)
                    thumbnailJobs.Add(new ThumbnailJob(record.Id, path, ForceRegenerate: true));

                summary.UpdatedFiles++;

                if (updates.Count >= DbBatchSize)
                {
                    await FlushUpdatesAsync(updates, cancellationToken).ConfigureAwait(false);
                    updates.Clear();
                }
            }
        }

        if (newRecords.Count > 0)
            await FlushNewRecordsAsync(newRecords, thumbnailJobs, cancellationToken).ConfigureAwait(false);

        if (updates.Count > 0)
            await FlushUpdatesAsync(updates, cancellationToken).ConfigureAwait(false);

        var missingIds = existing.Values
            .Where(p => !p.IsMissing && !diskPaths.Contains(p.FilePath))
            .Select(p => p.Id)
            .ToList();

        if (missingIds.Count > 0)
        {
            await Task.Run(() => _photos.MarkMissingBatch(missingIds), cancellationToken).ConfigureAwait(false);
            summary.MissingFiles += missingIds.Count;
        }

        await ProcessThumbnailJobsAsync(thumbnailJobs, cancellationToken).ConfigureAwait(false);
    }

    private async Task FlushNewRecordsAsync(
        List<NewPhotoRecord> records,
        List<ThumbnailJob> thumbnailJobs,
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

            thumbnailJobs.Add(new ThumbnailJob(saved.Id, saved.FilePath, ForceRegenerate: true));
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

    private async Task ProcessThumbnailJobsAsync(List<ThumbnailJob> jobs, CancellationToken cancellationToken)
    {
        if (jobs.Count == 0)
            return;

        var distinct = jobs.GroupBy(j => j.PhotoId).Select(g => g.Last()).ToList();
        jobs.Clear();

        for (var i = 0; i < distinct.Count; i += ThumbnailBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = distinct.Skip(i).Take(ThumbnailBatchSize).ToList();
            var thumbUpdates = new List<ThumbnailPathUpdate>();

            foreach (var job in batch)
            {
                var paths = await _thumbnails.EnsureThumbnailsAsync(
                    job.PhotoId,
                    job.SourcePath,
                    job.ForceRegenerate,
                    cancellationToken).ConfigureAwait(false);

                if (paths.SmallPath is null)
                    continue;

                thumbUpdates.Add(new ThumbnailPathUpdate
                {
                    PhotoId = job.PhotoId,
                    SmallPath = paths.SmallPath,
                    MediumPath = paths.MediumPath
                });
            }

            if (thumbUpdates.Count > 0)
            {
                await Task.Run(() => _photos.UpdateThumbnailPathsBatch(thumbUpdates), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
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

        public SyncSummary ToSummary() => new(NewFiles, UpdatedFiles, MissingFiles, ProcessedFiles);
    }

    private readonly record struct ThumbnailJob(long PhotoId, string SourcePath, bool ForceRegenerate);
}

public sealed record SyncSummary(int NewFiles, int UpdatedFiles, int MissingFiles, int ProcessedFiles)
{
    public static SyncSummary Skipped() => new(0, 0, 0, 0);
}
