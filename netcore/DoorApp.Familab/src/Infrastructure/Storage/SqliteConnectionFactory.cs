using DoorApp.Familab.Application.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace DoorApp.Familab.Infrastructure.Storage;

/// <summary>Creates SQLite connections to the configured database file, ensuring its directory exists.</summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IOptions<DoorOptions> options)
    {
        var path = options.Value.Storage.SqlitePath;
        var fullPath = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    /// <summary>Test/explicit constructor with a direct connection string (e.g. in-memory).</summary>
    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}
