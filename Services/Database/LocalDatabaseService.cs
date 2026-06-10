using Microsoft.Data.Sqlite;

namespace Lumen.Services.Database;

/// <summary>
/// SQLite connection factory and schema migrations.
/// </summary>
public sealed class LocalDatabaseService : IDisposable
{
    private readonly string _connectionString;
    private readonly object _initGate = new();
    private bool _initialized;

    public LocalDatabaseService()
    {
        LumenAppPaths.EnsureDirectories();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = LumenAppPaths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public SqliteConnection OpenConnection()
    {
        EnsureInitialized();
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void EnsureInitialized()
    {
        lock (_initGate)
        {
            if (_initialized)
                return;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            ApplyMigrations(connection);
            _initialized = true;
        }
    }

    private static void ApplyMigrations(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA temp_store = MEMORY;
            PRAGMA mmap_size = 268435456;
            PRAGMA cache_size = -64000;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS Folders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Path TEXT NOT NULL UNIQUE COLLATE NOCASE,
                DisplayName TEXT NOT NULL,
                LastScannedAt TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS Photos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FolderId INTEGER NOT NULL REFERENCES Folders(Id) ON DELETE CASCADE,
                FilePath TEXT NOT NULL UNIQUE COLLATE NOCASE,
                FileName TEXT NOT NULL,
                Extension TEXT,
                FileSize INTEGER NOT NULL,
                DateCreated TEXT,
                DateModified TEXT NOT NULL,
                DateTaken TEXT,
                Width INTEGER,
                Height INTEGER,
                Orientation INTEGER,
                CameraModel TEXT,
                Hash TEXT,
                ThumbnailPath TEXT,
                ThumbnailMediumPath TEXT,
                IsMissing INTEGER NOT NULL DEFAULT 0,
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                Rating INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Photos_FolderId ON Photos(FolderId);
            CREATE INDEX IF NOT EXISTS IX_Photos_IsMissing ON Photos(IsMissing);
            CREATE INDEX IF NOT EXISTS IX_Photos_VisibleSort ON Photos(
                IsMissing,
                DateCreated DESC,
                DateTaken DESC,
                DateModified DESC
            );

            CREATE TABLE IF NOT EXISTS ScanState (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                LastFullScanAt TEXT,
                LastIncrementalScanAt TEXT,
                AppVersion TEXT
            );

            INSERT OR IGNORE INTO ScanState (Id) VALUES (1);
            """;
        command.ExecuteNonQuery();
        EnsureScanStateColumns(connection);
    }

    private static void EnsureScanStateColumns(SqliteConnection connection)
    {
        EnsureColumn(connection, "ScanState", "CatalogRepairVersion", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "ScanState", "LastSyncError", "TEXT");
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        using var info = connection.CreateCommand();
        info.CommandText = $"PRAGMA table_info({table});";

        using var reader = info.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }

    public void Dispose()
    {
    }
}
