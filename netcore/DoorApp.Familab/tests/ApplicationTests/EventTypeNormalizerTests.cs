using DoorApp.Familab.Application.Services;
using Xunit;

namespace DoorApp.Familab.Tests.ApplicationTests;

public class EventTypeNormalizerTests
{
    [Theory]
    [InlineData("Badge Scan", "scan")]
    [InlineData("Door OPEN/UNLOCKED", "open")]
    [InlineData("Door CLOSED/LOCKED", "close")]
    [InlineData("Manual Unlock (1 hour)", "manual_unlock")]
    [InlineData("Manual Lock", "manual_lock")]
    [InlineData("Some Other Event", "some_other_event")]
    public void Normalize_matches_python_tokens(string input, string expected)
    {
        Assert.Equal(expected, EventTypeNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_null_returns_unknown()
    {
        Assert.Equal("unknown", EventTypeNormalizer.Normalize(null));
    }
}
