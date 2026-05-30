using DoorApp.Familab.Application.Services;
using DoorApp.Familab.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoorApp.Familab.Tests.ApplicationTests;

public class BadgeValidationServiceTests
{
    private static BadgeValidationService Build(IBadgeStore store, out RuntimeStatus status)
    {
        status = new RuntimeStatus(new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        return new BadgeValidationService(store, status, NullLogger<BadgeValidationService>.Instance);
    }

    [Fact]
    public async Task Known_badge_is_granted()
    {
        var svc = Build(new InMemoryBadgeStore("DEADBEEF"), out _);
        var outcome = await svc.ValidateAsync("deadbeef");
        Assert.True(outcome.Granted);
        Assert.Equal("deadbeef", outcome.Uid);
    }

    [Fact]
    public async Task Unknown_badge_is_denied()
    {
        var svc = Build(new InMemoryBadgeStore("deadbeef"), out _);
        var outcome = await svc.ValidateAsync("0badc0de");
        Assert.False(outcome.Granted);
    }

    [Fact]
    public async Task Uid_is_normalized_case_insensitively()
    {
        var svc = Build(new InMemoryBadgeStore("abcd1234"), out _);
        var outcome = await svc.ValidateAsync("  ABCD1234 ");
        Assert.True(outcome.Granted);
    }

    [Fact]
    public async Task Successful_lookup_records_data_connection()
    {
        var svc = Build(new InMemoryBadgeStore("abcd1234"), out var status);
        await svc.ValidateAsync("abcd1234");
        Assert.NotNull(status.LastDataConnection);
    }
}
