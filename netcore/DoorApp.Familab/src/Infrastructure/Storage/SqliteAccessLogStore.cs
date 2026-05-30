using DoorApp.Familab.Domain;
using DoorApp.Familab.Domain.Models;
using Microsoft.Data.Sqlite;

namespace DoorApp.Familab.Infrastructure.Storage;

/// <summary>
/// SQLite-backed access/audit event store. Replaces the Python monthly SQLite metrics
/// database. Timestamps are stored as "yyyy-MM-dd HH:mm:ss" so range queries work
/// with lexicographic comparisons (same convention as the Python project).
/// </summary>
public sealed class SqliteAccessLogStore : IAccessLogStore
{
    internal const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly SqliteConnectionFactory _factory;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public SqliteAccessLogStore(SqliteConnectionFactory factory)
    {
        _factory = factory;
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var connection = _factory.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ts TEXT NOT NULL,
                event_type TEXT NOT NULL,
                badge_id TEXT,
                status TEXT NOT NULL,
                raw_message TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_events_ts ON events(ts);
            CREATE INDEX IF NOT EXISTS idx_events_event_type ON events(event_type);
            CREATE INDEX IF NOT EXISTS idx_events_badge_id ON events(badge_id);
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task AppendAsync(AccessEvent accessEvent, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = _factory.Open();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO events (ts, event_type, badge_id, status, raw_message)
                VALUES ($ts, $type, $badge, $status, $raw);
                """;
            cmd.Parameters.AddWithValue("$ts", accessEvent.Timestamp.ToString(TimestampFormat));
            cmd.Parameters.AddWithValue("$type", accessEvent.EventType);
            cmd.Parameters.AddWithValue("$badge", (object?)accessEvent.BadgeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$status", accessEvent.Status);
            cmd.Parameters.AddWithValue("$raw", accessEvent.RawMessage);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<AccessEvent>> QueryRangeAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyCollection<string>? eventTypes = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<AccessEvent>();
        await using var connection = _factory.Open();
        await using var cmd = connection.CreateCommand();

        var sql = "SELECT id, ts, event_type, badge_id, status, raw_message FROM events WHERE ts >= $start AND ts <= $end";
        cmd.Parameters.AddWithValue("$start", start.ToString(TimestampFormat));
        cmd.Parameters.AddWithValue("$end", end.ToString(TimestampFormat));

        if (eventTypes is { Count: > 0 })
        {
            var names = new List<string>();
            var i = 0;
            foreach (var type in eventTypes)
            {
                var name = $"$t{i++}";
                names.Add(name);
                cmd.Parameters.AddWithValue(name, type);
            }
            sql += $" AND event_type IN ({string.Join(",", names)})";
        }

        sql += " ORDER BY ts ASC, id ASC;";
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new AccessEvent
            {
                Id = reader.GetInt64(0),
                Timestamp = ParseTimestamp(reader.GetString(1)),
                EventType = reader.GetString(2),
                BadgeId = reader.IsDBNull(3) ? null : reader.GetString(3),
                Status = reader.GetString(4),
                RawMessage = reader.GetString(5)
            });
        }

        return results;
    }

    private static DateTimeOffset ParseTimestamp(string raw)
    {
        if (DateTimeOffset.TryParseExact(raw, TimestampFormat, null,
                System.Globalization.DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.TryParse(raw, out var fallback) ? fallback : DateTimeOffset.MinValue;
    }
}
