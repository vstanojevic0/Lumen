using Lumen.Core.Models;
using Microsoft.Data.Sqlite;

namespace Lumen.Services.Database;

public sealed class PhotoRepository
{
    private readonly LocalDatabaseService _database;

    public PhotoRepository(LocalDatabaseService database) => _database = database;

    public IReadOnlyList<CatalogPhotoRecord> GetAllVisible()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id, FolderId, FilePath, FileName, Extension, FileSize,
                DateCreated, DateModified, DateTaken, Width, Height, Orientation,
                CameraModel, Hash, ThumbnailPath, ThumbnailMediumPath,
                IsMissing, IsFavorite, Rating, CreatedAt, UpdatedAt
            FROM Photos
            WHERE IsMissing = 0
            ORDER BY COALESCE(DateCreated, DateTaken, DateModified) DESC, FilePath COLLATE NOCASE;
            """;

        return ReadAll(command);
    }

    public CatalogPhotoRecord? GetByPath(string filePath)
    {
        filePath = CatalogPathNormalizer.NormalizeFilePath(filePath);
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id, FolderId, FilePath, FileName, Extension, FileSize,
                DateCreated, DateModified, DateTaken, Width, Height, Orientation,
                CameraModel, Hash, ThumbnailPath, ThumbnailMediumPath,
                IsMissing, IsFavorite, Rating, CreatedAt, UpdatedAt
            FROM Photos
            WHERE FilePath = $path COLLATE NOCASE
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$path", filePath);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadPhoto(reader) : null;
    }

    public Dictionary<string, CatalogPhotoRecord> GetByFolderId(long folderId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id, FolderId, FilePath, FileName, Extension, FileSize,
                DateCreated, DateModified, DateTaken, Width, Height, Orientation,
                CameraModel, Hash, ThumbnailPath, ThumbnailMediumPath,
                IsMissing, IsFavorite, Rating, CreatedAt, UpdatedAt
            FROM Photos
            WHERE FolderId = $folderId;
            """;
        command.Parameters.AddWithValue("$folderId", folderId);

        var map = new Dictionary<string, CatalogPhotoRecord>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var photo = ReadPhoto(reader);
            map[CatalogPathNormalizer.NormalizeFilePath(photo.FilePath)] = photo;
        }

        return map;
    }

    public int CountVisible()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Photos WHERE IsMissing = 0;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void SetFavorite(string filePath, bool favorite)
    {
        filePath = CatalogPathNormalizer.NormalizeFilePath(filePath);
        var now = DateTimeOffset.UtcNow.ToString("O");
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Photos
            SET IsFavorite = $favorite, UpdatedAt = $updatedAt
            WHERE FilePath = $path COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$favorite", favorite ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", now);
        command.Parameters.AddWithValue("$path", filePath);
        command.ExecuteNonQuery();
    }

    public void ApplyFavoriteMigration(IReadOnlyCollection<string> favoritePaths)
    {
        if (favoritePaths.Count == 0)
            return;

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var path in favoritePaths)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Photos
                SET IsFavorite = 1, UpdatedAt = $updatedAt
                WHERE FilePath = $path COLLATE NOCASE;
                """;
            command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$path", CatalogPathNormalizer.NormalizeFilePath(path));
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public IReadOnlySet<string> GetFavoritePaths()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT FilePath FROM Photos
            WHERE IsFavorite = 1 AND IsMissing = 0;
            """;

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            set.Add(reader.GetString(0));

        return set;
    }

    public long InsertPhoto(NewPhotoRecord photo)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        BindInsert(command, photo, returningId: true);
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public void InsertPhotosBatch(IReadOnlyList<NewPhotoRecord> photos)
    {
        if (photos.Count == 0)
            return;

        var deduped = photos
            .GroupBy(p => CatalogPathNormalizer.NormalizeFilePath(p.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var photo in deduped)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            BindUpsert(command, photo);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void UpdateThumbnailPathsBatch(IReadOnlyList<ThumbnailPathUpdate> updates)
    {
        if (updates.Count == 0)
            return;

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow.ToString("O");

        foreach (var update in updates)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Photos
                SET ThumbnailPath = $small,
                    ThumbnailMediumPath = $medium,
                    UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$id", update.PhotoId);
            command.Parameters.AddWithValue("$small", (object?)update.SmallPath ?? DBNull.Value);
            command.Parameters.AddWithValue("$medium", (object?)update.MediumPath ?? DBNull.Value);
            command.Parameters.AddWithValue("$updatedAt", now);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void UpdatePhotosBatch(IReadOnlyList<PhotoUpdateRecord> updates)
    {
        if (updates.Count == 0)
            return;

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var update in updates)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Photos SET
                    FileSize = $fileSize,
                    DateCreated = $dateCreated,
                    DateModified = $dateModified,
                    DateTaken = $dateTaken,
                    Width = $width,
                    Height = $height,
                    Orientation = $orientation,
                    CameraModel = $cameraModel,
                    Hash = $hash,
                    ThumbnailPath = $thumbnailPath,
                    ThumbnailMediumPath = $thumbnailMediumPath,
                    IsMissing = 0,
                    UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$id", update.Id);
            command.Parameters.AddWithValue("$fileSize", update.FileSize);
            command.Parameters.AddWithValue("$dateCreated", ToDbDate(update.DateCreated));
            command.Parameters.AddWithValue("$dateModified", update.DateModified.UtcDateTime.ToString("O"));
            command.Parameters.AddWithValue("$dateTaken", ToDbDate(update.DateTaken));
            command.Parameters.AddWithValue("$width", (object?)update.Width ?? DBNull.Value);
            command.Parameters.AddWithValue("$height", (object?)update.Height ?? DBNull.Value);
            command.Parameters.AddWithValue("$orientation", (object?)update.Orientation ?? DBNull.Value);
            command.Parameters.AddWithValue("$cameraModel", (object?)update.CameraModel ?? DBNull.Value);
            command.Parameters.AddWithValue("$hash", (object?)update.Hash ?? DBNull.Value);
            command.Parameters.AddWithValue("$thumbnailPath", (object?)update.ThumbnailPath ?? DBNull.Value);
            command.Parameters.AddWithValue("$thumbnailMediumPath", (object?)update.ThumbnailMediumPath ?? DBNull.Value);
            command.Parameters.AddWithValue("$updatedAt", update.UpdatedAt.UtcDateTime.ToString("O"));
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void MarkMissingBatch(IReadOnlyList<long> photoIds)
    {
        if (photoIds.Count == 0)
            return;

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow.ToString("O");

        foreach (var id in photoIds)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Photos
                SET IsMissing = 1, UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$updatedAt", now);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void BindInsert(SqliteCommand command, NewPhotoRecord photo, bool returningId)
    {
        command.CommandText = """
            INSERT INTO Photos (
                FolderId, FilePath, FileName, Extension, FileSize,
                DateCreated, DateModified, DateTaken, Width, Height, Orientation,
                CameraModel, Hash, ThumbnailPath, ThumbnailMediumPath,
                IsMissing, IsFavorite, Rating, CreatedAt, UpdatedAt
            ) VALUES (
                $folderId, $filePath, $fileName, $extension, $fileSize,
                $dateCreated, $dateModified, $dateTaken, $width, $height, $orientation,
                $cameraModel, $hash, $thumbnailPath, $thumbnailMediumPath,
                0, $isFavorite, 0, $createdAt, $updatedAt
            )
            """ + (returningId ? "RETURNING Id;" : ";");
        BindInsertParameters(command, photo);
    }

    private static void BindUpsert(SqliteCommand command, NewPhotoRecord photo)
    {
        command.CommandText = """
            INSERT INTO Photos (
                FolderId, FilePath, FileName, Extension, FileSize,
                DateCreated, DateModified, DateTaken, Width, Height, Orientation,
                CameraModel, Hash, ThumbnailPath, ThumbnailMediumPath,
                IsMissing, IsFavorite, Rating, CreatedAt, UpdatedAt
            ) VALUES (
                $folderId, $filePath, $fileName, $extension, $fileSize,
                $dateCreated, $dateModified, $dateTaken, $width, $height, $orientation,
                $cameraModel, $hash, $thumbnailPath, $thumbnailMediumPath,
                0, $isFavorite, 0, $createdAt, $updatedAt
            )
            ON CONFLICT(FilePath) DO UPDATE SET
                FolderId = excluded.FolderId,
                FileName = excluded.FileName,
                Extension = excluded.Extension,
                FileSize = excluded.FileSize,
                DateCreated = excluded.DateCreated,
                DateModified = excluded.DateModified,
                DateTaken = excluded.DateTaken,
                Width = excluded.Width,
                Height = excluded.Height,
                Orientation = excluded.Orientation,
                CameraModel = excluded.CameraModel,
                Hash = excluded.Hash,
                ThumbnailPath = COALESCE(Photos.ThumbnailPath, excluded.ThumbnailPath),
                ThumbnailMediumPath = COALESCE(Photos.ThumbnailMediumPath, excluded.ThumbnailMediumPath),
                IsMissing = 0,
                UpdatedAt = excluded.UpdatedAt;
            """;
        BindInsertParameters(command, photo);
    }

    private static void BindInsertParameters(SqliteCommand command, NewPhotoRecord photo)
    {
        command.Parameters.AddWithValue("$folderId", photo.FolderId);
        command.Parameters.AddWithValue("$filePath", CatalogPathNormalizer.NormalizeFilePath(photo.FilePath));
        command.Parameters.AddWithValue("$fileName", photo.FileName);
        command.Parameters.AddWithValue("$extension", (object?)photo.Extension ?? DBNull.Value);
        command.Parameters.AddWithValue("$fileSize", photo.FileSize);
        command.Parameters.AddWithValue("$dateCreated", ToDbDate(photo.DateCreated));
        command.Parameters.AddWithValue("$dateModified", photo.DateModified.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$dateTaken", ToDbDate(photo.DateTaken));
        command.Parameters.AddWithValue("$width", (object?)photo.Width ?? DBNull.Value);
        command.Parameters.AddWithValue("$height", (object?)photo.Height ?? DBNull.Value);
        command.Parameters.AddWithValue("$orientation", (object?)photo.Orientation ?? DBNull.Value);
        command.Parameters.AddWithValue("$cameraModel", (object?)photo.CameraModel ?? DBNull.Value);
        command.Parameters.AddWithValue("$hash", (object?)photo.Hash ?? DBNull.Value);
        command.Parameters.AddWithValue("$thumbnailPath", (object?)photo.ThumbnailPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$thumbnailMediumPath", (object?)photo.ThumbnailMediumPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$isFavorite", photo.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", photo.CreatedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", photo.UpdatedAt.UtcDateTime.ToString("O"));
    }

    private static List<CatalogPhotoRecord> ReadAll(SqliteCommand command)
    {
        var list = new List<CatalogPhotoRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            list.Add(ReadPhoto(reader));
        return list;
    }

    private static CatalogPhotoRecord ReadPhoto(SqliteDataReader reader) =>
        new()
        {
            Id = reader.GetInt64(0),
            FolderId = reader.GetInt64(1),
            FilePath = reader.GetString(2),
            FileName = reader.GetString(3),
            Extension = reader.IsDBNull(4) ? null : reader.GetString(4),
            FileSize = reader.GetInt64(5),
            DateCreated = reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6)),
            DateModified = ParseDate(reader.GetString(7)) ?? DateTimeOffset.MinValue,
            DateTaken = reader.IsDBNull(8) ? null : ParseDate(reader.GetString(8)),
            Width = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            Height = reader.IsDBNull(10) ? null : reader.GetInt32(10),
            Orientation = reader.IsDBNull(11) ? null : reader.GetInt32(11),
            CameraModel = reader.IsDBNull(12) ? null : reader.GetString(12),
            Hash = reader.IsDBNull(13) ? null : reader.GetString(13),
            ThumbnailPath = reader.IsDBNull(14) ? null : reader.GetString(14),
            ThumbnailMediumPath = reader.IsDBNull(15) ? null : reader.GetString(15),
            IsMissing = reader.GetInt64(16) != 0,
            IsFavorite = reader.GetInt64(17) != 0,
            Rating = (int)reader.GetInt64(18),
            CreatedAt = ParseDate(reader.GetString(19)) ?? DateTimeOffset.MinValue,
            UpdatedAt = ParseDate(reader.GetString(20)) ?? DateTimeOffset.MinValue
        };

    private static object ToDbDate(DateTimeOffset? value) =>
        value is null ? DBNull.Value : value.Value.UtcDateTime.ToString("O");

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
}

public sealed class NewPhotoRecord
{
    public long FolderId { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string? Extension { get; init; }
    public long FileSize { get; init; }
    public DateTimeOffset? DateCreated { get; init; }
    public DateTimeOffset DateModified { get; init; }
    public DateTimeOffset? DateTaken { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? Orientation { get; init; }
    public string? CameraModel { get; init; }
    public string? Hash { get; init; }
    public string? ThumbnailPath { get; init; }
    public string? ThumbnailMediumPath { get; init; }
    public bool IsFavorite { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class ThumbnailPathUpdate
{
    public long PhotoId { get; init; }
    public string? SmallPath { get; init; }
    public string? MediumPath { get; init; }
}

public sealed class PhotoUpdateRecord
{
    public long Id { get; init; }
    public long FileSize { get; init; }
    public DateTimeOffset? DateCreated { get; init; }
    public DateTimeOffset DateModified { get; init; }
    public DateTimeOffset? DateTaken { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? Orientation { get; init; }
    public string? CameraModel { get; init; }
    public string? Hash { get; init; }
    public string? ThumbnailPath { get; init; }
    public string? ThumbnailMediumPath { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
