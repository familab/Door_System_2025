using DoorApp.Familab.Domain.Models;
using DoorApp.Familab.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DoorApp.Familab.Tests.InfrastructureTests;

/// <summary>
/// Integration tests for the SQLite storage layer using a real temp-file database
/// (no hardware required).
/// </summary>
public sealed class SqliteStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public SqliteStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"doorapp-test-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
        _factory = new SqliteConnectionFactory(connectionString);
    }

    [Fact]
    public async Task BadgeStore_add_contains_remove()
    {
        var store = new SqliteBadgeStore(_factory);
        Assert.True(await store.AddAsync("DEADBEEF"));
        Assert.False(await store.AddAsync("deadbeef")); // duplicate (normalized)
        Assert.True(await store.ContainsAsync("deadbeef"));
        Assert.True(await store.RemoveAsync("DEADBEEF"));
        Assert.False(await store.ContainsAsync("deadbeef"));
    }

    [Fact]
    public async Task BadgeStore_replace_all()
    {
        var store = new SqliteBadgeStore(_factory);
        await store.AddAsync("old");
        var count = await store.ReplaceAllAsync(new[] { "AA", "bb", "AA" });
        Assert.Equal(2, count);
        Assert.False(await store.ContainsAsync("old"));
        Assert.True(await store.ContainsAsync("aa"));
    }

    [Fact]
    public async Task AccessLogStore_append_and_query_range()
    {
        var store = new SqliteAccessLogStore(_factory);
        var t0 = DateTimeOffset.Parse("2026-05-29T10:00:00");
        await store.AppendAsync(new AccessEvent { Timestamp = t0, EventType = "scan", Status = "granted", BadgeId = "aa", RawMessage = "x" });
        await store.AppendAsync(new AccessEvent { Timestamp = t0.AddHours(1), EventType = "open", Status = "success", RawMessage = "y" });

        var all = await store.QueryRangeAsync(t0.AddDays(-1), t0.AddDays(1));
        Assert.Equal(2, all.Count);

        var scansOnly = await store.QueryRangeAsync(t0.AddDays(-1), t0.AddDays(1), new[] { "scan" });
        Assert.Single(scansOnly);
        Assert.Equal("aa", scansOnly[0].BadgeId);
    }

    [Fact]
    public async Task AccessLogStore_range_excludes_outside_events()
    {
        var store = new SqliteAccessLogStore(_factory);
        var inside = DateTimeOffset.Parse("2026-05-29T10:00:00");
        var outside = DateTimeOffset.Parse("2020-01-01T10:00:00");
        await store.AppendAsync(new AccessEvent { Timestamp = inside, EventType = "scan", Status = "granted", RawMessage = "x" });
        await store.AppendAsync(new AccessEvent { Timestamp = outside, EventType = "scan", Status = "granted", RawMessage = "x" });

        var result = await store.QueryRangeAsync(DateTimeOffset.Parse("2026-05-01T00:00:00"), DateTimeOffset.Parse("2026-05-31T23:59:59"));
        Assert.Single(result);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best effort */ }
    }
}
