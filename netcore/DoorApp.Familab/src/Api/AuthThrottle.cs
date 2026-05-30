using System.Collections.Concurrent;
using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Domain;
using Microsoft.Extensions.Options;

namespace DoorApp.Familab.Api;

/// <summary>
/// In-memory per-IP auth-failure throttle. Ports Python auth.record_auth_failure /
/// is_throttled: after a failed login the source IP is blocked for a short window.
/// </summary>
public sealed class AuthThrottle
{
    private readonly ISystemClock _clock;
    private readonly IOptionsMonitor<DoorOptions> _options;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _blockedUntil = new();

    public AuthThrottle(ISystemClock clock, IOptionsMonitor<DoorOptions> options)
    {
        _clock = clock;
        _options = options;
    }

    public void RecordFailure(string ip)
    {
        var seconds = Math.Max(0, _options.CurrentValue.Auth.FailThrottleSeconds);
        _blockedUntil[ip] = _clock.Now.AddSeconds(seconds);
    }

    public bool IsThrottled(string ip)
    {
        if (_blockedUntil.TryGetValue(ip, out var until))
        {
            if (until > _clock.Now)
            {
                return true;
            }
            _blockedUntil.TryRemove(ip, out _);
        }

        return false;
    }
}
