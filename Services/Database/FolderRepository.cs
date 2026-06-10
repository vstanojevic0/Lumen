using Lumen.Core.Models;
using Microsoft.Data.Sqlite;

namespace Lumen.Services.Database;

public sealed class FolderRepository
{
    private readonly LocalDatabaseService _database;

    public FolderRepository(LocalDatabaseService database) => _database = database;

    public IReadOnlyList<CatalogFolderRecord> GetActiveFolders()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Path, DisplayName, LastScannedAt, IsActive
            FROM Folders
            WHERE IsActive = 1
            ORDER BY Path COLLATE NOCASE;
            """;

        var list = new List<CatalogFolderRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            list.Add(ReadFolder(reader));

        return list;
    }

    public CatalogFolderRecord? GetByPath(string path)
    {
        path = NormalizeFolderPath(path);
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Path, DisplayName, LastScannedAt, IsActive
            FROM Folders
            WHERE Path = $path COLLATE NOCASE
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$path", path);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadFolder(reader) : null;
    }

    public long UpsertFolder(string path, string displayName, bool isActive = true)
    {
        path = NormalizeFolderPath(path);
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Folders (Path, DisplayName, IsActive)
            VALUES ($path, $displayName, $isActive)
            ON CONFLICT(Path) DO UPDATE SET
                DisplayName = excluded.DisplayName,
                IsActive = excluded.IsActive;
            SELECT Id FROM Folders WHERE Path = $path COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$displayName", displayName);
        command.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);

        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public void SyncScanRoots(IReadOnlyList<string> scanRoots)
    {
        var normalized = CatalogPathNormalizer.PruneNestedScanRoots(scanRoots);

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var path in normalized)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Folders (Path, DisplayName, IsActive)
                VALUES ($path, $displayName, 1)
                ON CONFLICT(Path) DO UPDATE SET IsActive = 1;
                """;
            command.Parameters.AddWithValue("$path", path);
            command.Parameters.AddWithValue("$displayName", FormatDisplayName(path));
            command.ExecuteNonQuery();
        }

        using (var deactivate = connection.CreateCommand())
        {
            deactivate.Transaction = transaction;
            if (normalized.Count == 0)
            {
                deactivate.CommandText = "UPDATE Folders SET IsActive = 0;";
            }
            else
            {
                deactivate.CommandText = """
                    UPDATE Folders
                    SET IsActive = 0
                    WHERE Path NOT IN ($paths);
                    """;
                var placeholders = new List<string>();
                for (var i = 0; i < normalized.Count; i++)
                {
                    var name = $"$p{i}";
                    placeholders.Add(name);
                    deactivate.Parameters.AddWithValue(name, normalized[i]);
                }

                deactivate.CommandText = deactivate.CommandText.Replace("$paths", string.Join(", ", placeholders));
            }

            deactivate.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void SetLastScannedAt(long folderId, DateTimeOffset scannedAt)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Folders SET LastScannedAt = $at WHERE Id = $id;";
        command.Parameters.AddWithValue("$at", scannedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$id", folderId);
        command.ExecuteNonQuery();
    }

    private static CatalogFolderRecord ReadFolder(SqliteDataReader reader) =>
        new()
        {
            Id = reader.GetInt64(0),
            Path = reader.GetString(1),
            DisplayName = reader.GetString(2),
            LastScannedAt = reader.IsDBNull(3) ? null : ParseDate(reader.GetString(3)),
            IsActive = reader.GetInt64(4) != 0
        };

    private static string NormalizeFolderPath(string path) =>
        CatalogPathNormalizer.NormalizeFolderPath(path);

    private static string FormatDisplayName(string path)
    {
        var leaf = Path.GetFileName(path);
        return string.IsNullOrEmpty(leaf) ? path : leaf;
    }

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
}
