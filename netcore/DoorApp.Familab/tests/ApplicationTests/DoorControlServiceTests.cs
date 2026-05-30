using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Application.Services;
using DoorApp.Familab.Infrastructure.Hardware;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoorApp.Familab.Tests.ApplicationTests;

public class DoorControlServiceTests
{
    private static (DoorControlService Service, StubDoorRelay Relay, FakeActionLog Log) Build()
    {
        var relay = new StubDoorRelay(NullLogger<StubDoorRelay>.Instance);
        var log = new FakeActionLog();
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var options = new TestOptionsMonitor<DoorOptions>(TestData.DefaultOptions());
        var service = new DoorControlService(relay, log, clock, options, NullLogger<DoorControlService>.Instance);
        return (service, relay, log);
    }

    [Fact]
    public void Starts_locked()
    {
        var (service, relay, _) = Build();
        Assert.False(service.State.IsOpen);
        Assert.False(relay.Energized);
    }

    [Fact]
    public async Task Unlock_energizes_relay_and_records_event()
    {
        var (service, relay, log) = Build();
        await service.UnlockAsync(TimeSpan.FromHours(1));

        Assert.True(service.State.IsOpen);
        Assert.True(relay.Energized);
        Assert.Contains(log.Entries, e => e.Action == "Door OPEN/UNLOCKED");
    }

    [Fact]
    public async Task Lock_deenergizes_relay_and_records_event()
    {
        var (service, relay, log) = Build();
        await service.UnlockAsync(TimeSpan.FromHours(1));
        await service.LockAsync();

        Assert.False(service.State.IsOpen);
        Assert.False(relay.Energized);
        Assert.Contains(log.Entries, e => e.Action == "Door CLOSED/LOCKED");
    }

    [Fact]
    public async Task Toggle_from_locked_unlocks()
    {
        var (service, _, log) = Build();
        var result = await service.ToggleAsync("user=test");

        Assert.Equal("unlocked", result);
        Assert.True(service.State.IsOpen);
        Assert.Contains(log.Entries, e => e.Action == "Manual Unlock (1 hour)" && e.BadgeId == "user=test");
    }

    [Fact]
    public async Task Toggle_from_unlocked_locks()
    {
        var (service, _, log) = Build();
        await service.UnlockAsync(TimeSpan.FromHours(1));
        var result = await service.ToggleAsync();

        Assert.Equal("locked", result);
        Assert.False(service.State.IsOpen);
        Assert.Contains(log.Entries, e => e.Action == "Manual Lock");
    }

    [Fact]
    public async Task Temporary_unlock_auto_relocks_after_duration()
    {
        var (service, relay, _) = Build();
        await service.UnlockTemporarilyAsync(TimeSpan.FromMilliseconds(100), "badge-1");
        Assert.True(service.State.IsOpen);

        // Wait past the relock timer.
        await Task.Delay(400);
        Assert.False(service.State.IsOpen);
        Assert.False(relay.Energized);
    }
}
