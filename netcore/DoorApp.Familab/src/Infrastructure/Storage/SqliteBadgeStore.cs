using DoorApp.Familab.Domain;
using Microsoft.Data.Sqlite;

namespace DoorApp.Familab.Infrastructure.Storage;

/// <summary>SQLite-backed authorised-badge store. Replaces the Python local CSV badge list.</summary>
public sealed class SqliteBadgeStore : IBadgeStore
{
    private readonly SqliteConnectionFactory _factory;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public SqliteBadgeStore(SqliteConnectionFactory factory)
    {
        _factory = factory;
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var connection = _factory.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS badges (uid TEXT PRIMARY KEY NOT NULL);";
        cmd.ExecuteNonQuery();
    }

    public async Task<bool> ContainsAsync(string uid, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(uid);
        await using var connection = _factory.Open();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM badges WHERE uid = $uid LIMIT 1;";
        cmd.Parameters.AddWithValue("$uid", normalized);
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    public async Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        await using var connection = _factory.Open();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT uid FROM badges ORDER BY uid ASC;";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(reader.GetString(0));
        }

        return list;
    }

    public async Task<bool> AddAsync(string uid, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(uid);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = _factory.Open();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO badges (uid) VALUES ($uid);";
            cmd.Parameters.AddWithValue("$uid", normalized);
            var rows = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return rows > 0;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> RemoveAsync(string uid, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(uid);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = _factory.Open();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM badges WHERE uid = $uid;";
            cmd.Parameters.AddWithValue("$uid", normalized);
            var rows = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return rows > 0;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<int> ReplaceAllAsync(IEnumerable<string> uids, CancellationToken cancellationToken = default)
    {
        var normalized = uids.Select(Normalize).Where(u => u.Length > 0).Distinct().ToList();
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = _factory.Open();
            await using var tx = connection.BeginTransaction();

            await using (var clear = connection.CreateCommand())
            {
                clear.Transaction = tx;
                clear.CommandText = "DELETE FROM badges;";
                await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText = "INSERT OR IGNORE INTO badges (uid) VALUES ($uid);";
                var param = insert.CreateParameter();
                param.ParameterName = "$uid";
                insert.Parameters.Add(param);
                foreach (var uid in normalized)
                {
                    param.Value = uid;
                    await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return normalized.Count;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static string Normalize(string uid) => (uid ?? string.Empty).Trim().ToLowerInvariant();
}
