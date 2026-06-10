using Lumen.Core.Models;
using Microsoft.Data.Sqlite;

namespace Lumen.Services.Database;

public sealed class ScanStateRepository
{
    private readonly LocalDatabaseService _database;

    public ScanStateRepository(LocalDatabaseService database) => _database = database;

    public ScanStateRecord Get()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, LastFullScanAt, LastIncrementalScanAt, AppVersion, CatalogRepairVersion, LastSyncError
            FROM ScanState
            WHERE Id = 1;
            """;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return new ScanStateRecord();

        return ReadState(reader);
    }

    public void SetIncrementalScanAt(DateTimeOffset scannedAt, string appVersion)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ScanState
            SET LastIncrementalScanAt = $at,
                AppVersion = $version,
                LastSyncError = NULL
            WHERE Id = 1;
            """;
        command.Parameters.AddWithValue("$at", scannedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$version", appVersion);
        command.ExecuteNonQuery();
    }

    public void SetFullScanAt(DateTimeOffset scannedAt, string appVersion)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ScanState
            SET LastFullScanAt = $fullAt,
                LastIncrementalScanAt = $fullAt,
                AppVersion = $version,
                LastSyncError = NULL
            WHERE Id = 1;
            """;
        command.Parameters.AddWithValue("$fullAt", scannedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$version", appVersion);
        command.ExecuteNonQuery();
    }

    public void SetLastSyncError(string message)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ScanState
            SET LastSyncError = $error
            WHERE Id = 1;
            """;
        command.Parameters.AddWithValue("$error", message);
        command.ExecuteNonQuery();
    }

    public void ClearLastSyncError()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ScanState SET LastSyncError = NULL WHERE Id = 1;";
        command.ExecuteNonQuery();
    }

    public void SetCatalogRepairVersion(int version)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ScanState
            SET CatalogRepairVersion = $version
            WHERE Id = 1;
            """;
        command.Parameters.AddWithValue("$version", version);
        command.ExecuteNonQuery();
    }

    private static ScanStateRecord ReadState(SqliteDataReader reader) =>
        new()
        {
            Id = reader.GetInt64(0),
            LastFullScanAt = reader.IsDBNull(1) ? null : ParseDate(reader.GetString(1)),
            LastIncrementalScanAt = reader.IsDBNull(2) ? null : ParseDate(reader.GetString(2)),
            AppVersion = reader.IsDBNull(3) ? null : reader.GetString(3),
            CatalogRepairVersion = reader.FieldCount > 4 && !reader.IsDBNull(4) ? reader.GetInt32(4) : 0,
            LastSyncError = reader.FieldCount > 5 && !reader.IsDBNull(5) ? reader.GetString(5) : null
        };

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
}
