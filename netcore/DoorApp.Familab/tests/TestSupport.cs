using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Domain;
using Microsoft.Extensions.Options;

namespace DoorApp.Familab.Tests;

/// <summary>Deterministic clock for time-dependent tests.</summary>
public sealed class TestClock : ISystemClock
{
    public TestClock(DateTimeOffset start) => Now = start;
    public DateTimeOffset Now { get; set; }
    public void Advance(TimeSpan by) => Now = Now.Add(by);
}

/// <summary>Minimal IOptionsMonitor over a fixed value for unit tests.</summary>
public sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T value) => CurrentValue = value;
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

internal static class TestData
{
    public static DoorOptions DefaultOptions() => new()
    {
        Timing = new TimingOptions
        {
            UnlockDurationSeconds = 3600,
            BadgeUnlockDurationSeconds = 5
        },
        Auth = new AuthOptions
        {
            MasterUsername = "admin",
            // pbkdf2 of "changeme"
            MasterPasswordHash = "pbkdf2_sha256$100000$ABEiM0RVZneImqu8zd7v8A==$sb87PE5SVx+GvGG9tirFWcus4aBq3U/HPcKV5H66298="
        }
    };
}
