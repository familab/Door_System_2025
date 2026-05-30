using System.Collections.Concurrent;
using DoorApp.Familab.Application.Abstractions;
using DoorApp.Familab.Domain;
using DoorApp.Familab.Domain.Models;

namespace DoorApp.Familab.Tests;

/// <summary>Captures recorded actions for assertions.</summary>
public sealed class FakeActionLog : IActionLogService
{
    public List<(string Action, string? BadgeId, AccessStatus Status)> Entries { get; } = new();

    public Task RecordAsync(string action, string? badgeId = null, AccessStatus status = AccessStatus.Success, CancellationToken cancellationToken = default)
    {
        lock (Entries)
        {
            Entries.Add((action, badgeId, status));
        }
        return Task.CompletedTask;
    }
}

/// <summary>In-memory access log store for analytics tests.</summary>
public sealed class InMemoryAccessLogStore : IAccessLogStore
{
    private readonly ConcurrentBag<AccessEvent> _events = new();

    public Task AppendAsync(AccessEvent accessEvent, CancellationToken cancellationToken = default)
    {
        _events.Add(accessEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AccessEvent>> QueryRangeAsync(
        DateTimeOffset start, DateTimeOffset end,
        IReadOnlyCollection<string>? eventTypes = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<AccessEvent> query = _events
            .Where(e => e.Timestamp >= start && e.Timestamp <= end);
        if (eventTypes is { Count: > 0 })
        {
            query = query.Where(e => eventTypes.Contains(e.EventType));
        }
        return Task.FromResult<IReadOnlyList<AccessEvent>>(query.OrderBy(e => e.Timestamp).ToList());
    }
}

/// <summary>In-memory badge store for validation tests.</summary>
public sealed class InMemoryBadgeStore : IBadgeStore
{
    private readonly HashSet<string> _set = new(StringComparer.Ordinal);

    public InMemoryBadgeStore(params string[] uids)
    {
        foreach (var uid in uids)
        {
            _set.Add(uid.Trim().ToLowerInvariant());
        }
    }

    public Task<bool> ContainsAsync(string uid, CancellationToken cancellationToken = default)
        => Task.FromResult(_set.Contains(uid.Trim().ToLowerInvariant()));

    public Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(_set.ToList());

    public Task<bool> AddAsync(string uid, CancellationToken cancellationToken = default)
        => Task.FromResult(_set.Add(uid.Trim().ToLowerInvariant()));

    public Task<bool> RemoveAsync(string uid, CancellationToken cancellationToken = default)
        => Task.FromResult(_set.Remove(uid.Trim().ToLowerInvariant()));

    public Task<int> ReplaceAllAsync(IEnumerable<string> uids, CancellationToken cancellationToken = default)
    {
        _set.Clear();
        foreach (var uid in uids)
        {
            _set.Add(uid.Trim().ToLowerInvariant());
        }
        return Task.FromResult(_set.Count);
    }
}
