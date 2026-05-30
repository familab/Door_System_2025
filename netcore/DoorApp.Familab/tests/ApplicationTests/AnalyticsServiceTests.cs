using DoorApp.Familab.Application.Services;
using DoorApp.Familab.Domain.Models;
using Xunit;

namespace DoorApp.Familab.Tests.ApplicationTests;

public class AnalyticsServiceTests
{
    private static AccessEvent Event(string type, string status, string? badge, DateTimeOffset ts) => new()
    {
        Timestamp = ts,
        EventType = type,
        Status = status,
        BadgeId = badge,
        RawMessage = $"{type} {status}"
    };

    [Fact]
    public async Task Summarise_counts_scans_grants_and_denials()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-05-30T12:00:00Z"));
        var status = new RuntimeStatus(clock);
        var store = new InMemoryAccessLogStore();
        var day = DateTimeOffset.Parse("2026-05-29T09:00:00Z");

        await store.AppendAsync(Event("scan", "granted", "aa", day));
        await store.AppendAsync(Event("scan", "granted", "aa", day.AddMinutes(1)));
        await store.AppendAsync(Event("scan", "denied", "bb", day.AddMinutes(2)));
        await store.AppendAsync(Event("open", "success", "aa", day.AddMinutes(1)));
        await store.AppendAsync(Event("manual_unlock", "success", null, day.AddMinutes(3)));

        var svc = new AnalyticsService(store, status, clock);
        var summary = await svc.SummariseAsync(day.AddDays(-1), day.AddDays(1));

        Assert.Equal(3, summary.TotalScans);
        Assert.Equal(2, summary.GrantedScans);
        Assert.Equal(1, summary.DeniedScans);
        Assert.Equal(1, summary.DoorOpenCount);
        Assert.Equal(1, summary.ManualActions);
        Assert.Equal("aa", summary.TopBadges[0].BadgeId);
        Assert.Equal(2, summary.TopBadges[0].Count);
    }

    [Fact]
    public async Task Summarise_reports_uptime_from_runtime_status()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-05-30T12:00:00Z"));
        var status = new RuntimeStatus(clock);
        clock.Advance(TimeSpan.FromSeconds(90));

        var svc = new AnalyticsService(new InMemoryAccessLogStore(), status, clock);
        var summary = await svc.SummariseAsync(clock.Now.AddDays(-1), clock.Now);

        Assert.Equal(90, summary.UptimeSeconds);
    }
}
