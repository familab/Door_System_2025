using DoorApp.Familab.Domain.Models;
using Xunit;

namespace DoorApp.Familab.Tests.DomainTests;

public class DoorStateTests
{
    [Fact]
    public void Locked_state_reports_closed_display()
    {
        var now = DateTimeOffset.Now;
        var state = DoorState.Locked(now);
        Assert.False(state.IsOpen);
        Assert.Equal("CLOSED/LOCKED", state.DisplayStatus);
        Assert.Equal(now, state.UpdatedAt);
    }

    [Fact]
    public void Unlocked_state_reports_open_display()
    {
        var state = DoorState.Unlocked(DateTimeOffset.Now);
        Assert.True(state.IsOpen);
        Assert.Equal("OPEN/UNLOCKED", state.DisplayStatus);
    }
}

public class AccessStatusTests
{
    [Theory]
    [InlineData(AccessStatus.Granted, "Granted")]
    [InlineData(AccessStatus.Denied, "Denied")]
    [InlineData(AccessStatus.Success, "Success")]
    [InlineData(AccessStatus.Failure, "Failure")]
    public void ToToken_returns_expected_string(AccessStatus status, string expected)
    {
        Assert.Equal(expected, status.ToToken());
    }

    [Fact]
    public void BadgeScanOutcome_maps_status()
    {
        Assert.Equal(AccessStatus.Granted, new BadgeScanOutcome("ab", true, "x").Status);
        Assert.Equal(AccessStatus.Denied, new BadgeScanOutcome("ab", false, "x").Status);
    }
}
