using DoorApp.Familab.Application.Services;
using Xunit;

namespace DoorApp.Familab.Tests.ApplicationTests;

public class HealthServiceTests
{
    [Theory]
    [InlineData(0, 0, 0, 5, "5s")]
    [InlineData(0, 0, 3, 4, "3m 4s")]
    [InlineData(0, 2, 3, 4, "2h 3m 4s")]
    [InlineData(1, 2, 3, 4, "1d 2h 3m 4s")]
    public void FormatUptime_matches_python_format(int d, int h, int m, int s, string expected)
    {
        var uptime = new TimeSpan(d, h, m, s);
        Assert.Equal(expected, HealthService.FormatUptime(uptime));
    }
}
