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
            SELECT Id, LastFullScanAt, LastIncrementalScanAt, AppVersion
            FROM ScanState
            WHERE Id = 1;
            """;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return new ScanStateRecord();

        return new ScanStateRecord
        {
            Id = reader.GetInt64(0),
            LastFullScanAt = reader.IsDBNull(1) ? null : ParseDate(reader.GetString(1)),
            LastIncrementalScanAt = reader.IsDBNull(2) ? null : ParseDate(reader.GetString(2)),
            AppVersion = reader.IsDBNull(3) ? null : reader.GetString(3)
        };
    }

    public void SetIncrementalScanAt(DateTimeOffset scannedAt, string appVersion)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ScanState
            SET LastIncrementalScanAt = $at, AppVersion = $version
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
                AppVersion = $version
            WHERE Id = 1;
            """;
        command.Parameters.AddWithValue("$fullAt", scannedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$version", appVersion);
        command.ExecuteNonQuery();
    }

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
}
