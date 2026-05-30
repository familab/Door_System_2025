using DoorApp.Familab.Application.Abstractions;
using DoorApp.Familab.Domain;
using DoorApp.Familab.Domain.Models;

namespace DoorApp.Familab.Application.Services;

/// <summary>
/// Aggregates access events into analytics. Replaces the server-side portion of the
/// Python metrics dashboard (badge scans, denied scans, door cycles, top users, uptime).
/// </summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IAccessLogStore _store;
    private readonly IRuntimeStatus _status;
    private readonly ISystemClock _clock;

    public AnalyticsService(IAccessLogStore store, IRuntimeStatus status, ISystemClock clock)
    {
        _store = store;
        _status = status;
        _clock = clock;
    }

    public Task<IReadOnlyList<AccessEvent>> GetEventsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
        => _store.QueryRangeAsync(start, end, null, cancellationToken);

    public async Task<AnalyticsSummary> SummariseAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        var events = await _store.QueryRangeAsync(start, end, null, cancellationToken).ConfigureAwait(false);

        var scans = events.Where(e => e.EventType == "scan").ToList();
        var granted = scans.Count(e => e.Status == "granted");
        var denied = scans.Count(e => e.Status == "denied");

        var scansPerDay = scans
            .GroupBy(e => e.Timestamp.ToString("yyyy-MM-dd"))
            .OrderBy(g => g.Key)
            .Select(g => new DailyCount(g.Key, g.Count()))
            .ToList();

        var topBadges = scans
            .Where(e => e.Status == "granted" && !string.IsNullOrEmpty(e.BadgeId))
            .GroupBy(e => e.BadgeId!)
            .Select(g => new BadgeCount(g.Key, g.Count()))
            .OrderByDescending(b => b.Count)
            .Take(10)
            .ToList();

        var uptimeSeconds = (long)(_clock.Now - _status.StartedAt).TotalSeconds;

        return new AnalyticsSummary
        {
            RangeStart = start,
            RangeEnd = end,
            TotalScans = scans.Count,
            GrantedScans = granted,
            DeniedScans = denied,
            DoorOpenCount = events.Count(e => e.EventType == "open"),
            ManualActions = events.Count(e => e.EventType is "manual_lock" or "manual_unlock"),
            ErrorCount = events.Count(e => e.Status is "failure" or "error"),
            UptimeSeconds = uptimeSeconds,
            ScansPerDay = scansPerDay,
            TopBadges = topBadges
        };
    }
}
