using DoorApp.Familab.Application.Abstractions;
using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Domain;
using DoorApp.Familab.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoorApp.Familab.Application.Services;

/// <summary>
/// Thread-safe door controller. Ports Python door_control.DoorController together with
/// the set_door_status / get_door_status globals. HIGH relay = unlocked, LOW = locked.
/// Auto-relock is handled with <see cref="Timer"/> instead of threading.Timer.
/// </summary>
public sealed class DoorControlService : IDoorControlService, IDisposable
{
    private readonly IDoorRelay _relay;
    private readonly IActionLogService _actionLog;
    private readonly ISystemClock _clock;
    private readonly IOptionsMonitor<DoorOptions> _options;
    private readonly ILogger<DoorControlService> _logger;

    private readonly object _gate = new();
    private bool _isOpen;
    private DateTimeOffset _updatedAt;
    private Timer? _autoLockTimer;
    private bool _longUnlockActive;

    public DoorControlService(
        IDoorRelay relay,
        IActionLogService actionLog,
        ISystemClock clock,
        IOptionsMonitor<DoorOptions> options,
        ILogger<DoorControlService> logger)
    {
        _relay = relay;
        _actionLog = actionLog;
        _clock = clock;
        _options = options;
        _logger = logger;
        _updatedAt = clock.Now;

        // Start in the locked state, matching the Python startup sequence.
        _relay.DeEnergize();
    }

    public DoorState State
    {
        get { lock (_gate) return new DoorState(_isOpen, _updatedAt); }
    }

    public async Task UnlockAsync(TimeSpan? duration = null, string? badgeId = null, CancellationToken cancellationToken = default)
    {
        var dur = duration ?? TimeSpan.FromSeconds(_options.CurrentValue.Timing.UnlockDurationSeconds);
        bool transitioned;
        lock (_gate)
        {
            if (!_isOpen)
            {
                _relay.Energize();
                _isOpen = true;
                _updatedAt = _clock.Now;
                transitioned = true;
                _logger.LogInformation("Door unlocked for {Seconds} seconds", dur.TotalSeconds);
            }
            else
            {
                transitioned = false;
                _logger.LogInformation("Door already unlocked, refreshing timer to {Seconds} seconds", dur.TotalSeconds);
            }

            _longUnlockActive = true;
            ScheduleAutoLock(dur);
        }

        if (transitioned)
        {
            await _actionLog.RecordAsync("Door OPEN/UNLOCKED", badgeId, AccessStatus.Success, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task LockAsync(string? badgeId = null, CancellationToken cancellationToken = default)
    {
        bool transitioned;
        lock (_gate)
        {
            CancelAutoLock();
            _longUnlockActive = false;
            transitioned = LockCore();
        }

        if (transitioned)
        {
            await _actionLog.RecordAsync("Door CLOSED/LOCKED", badgeId, AccessStatus.Success, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task UnlockTemporarilyAsync(TimeSpan duration, string? badgeId = null, CancellationToken cancellationToken = default)
    {
        bool transitioned;
        lock (_gate)
        {
            transitioned = !_isOpen;
            _relay.Energize();
            _isOpen = true;
            _updatedAt = _clock.Now;
            _logger.LogInformation("Door unlocked temporarily for {Seconds} seconds", duration.TotalSeconds);

            // Separate relock timer that only acts if no longer-running manual unlock is active.
            var timer = new Timer(_ => RelockAfterTemporary(badgeId), null, duration, Timeout.InfiniteTimeSpan);
            // The temporary timer is fire-once; it disposes itself in the callback path.
            _temporaryTimer?.Dispose();
            _temporaryTimer = timer;
        }

        if (transitioned)
        {
            await _actionLog.RecordAsync("Door OPEN/UNLOCKED", badgeId, AccessStatus.Success, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string> ToggleAsync(string? badgeId = null, CancellationToken cancellationToken = default)
    {
        if (State.IsOpen)
        {
            await _actionLog.RecordAsync("Manual Lock", badgeId, AccessStatus.Success, cancellationToken).ConfigureAwait(false);
            await LockAsync(badgeId, cancellationToken).ConfigureAwait(false);
            return "locked";
        }

        await _actionLog.RecordAsync("Manual Unlock (1 hour)", badgeId, AccessStatus.Success, cancellationToken).ConfigureAwait(false);
        await UnlockAsync(null, badgeId, cancellationToken).ConfigureAwait(false);
        return "unlocked";
    }

    private Timer? _temporaryTimer;

    private void RelockAfterTemporary(string? badgeId)
    {
        bool transitioned = false;
        lock (_gate)
        {
            // Only relock if we're not inside a longer unlock period (matches Python relock guard).
            if (!_longUnlockActive)
            {
                transitioned = LockCore();
            }
        }

        if (transitioned)
        {
            _ = SafeRecordAsync("Door CLOSED/LOCKED", badgeId);
            _logger.LogInformation("Door auto-locked after temporary unlock");
        }
    }

    /// <summary>De-energise the relay and flip state to locked. Must be called under <see cref="_gate"/>.</summary>
    private bool LockCore()
    {
        if (!_isOpen)
        {
            return false;
        }

        _relay.DeEnergize();
        _isOpen = false;
        _updatedAt = _clock.Now;
        _logger.LogInformation("Door locked");
        return true;
    }

    /// <summary>Schedule (or refresh) the manual auto-lock timer. Must be called under <see cref="_gate"/>.</summary>
    private void ScheduleAutoLock(TimeSpan after)
    {
        CancelAutoLock();
        _autoLockTimer = new Timer(_ => OnAutoLockElapsed(), null, after, Timeout.InfiniteTimeSpan);
    }

    private void CancelAutoLock()
    {
        _autoLockTimer?.Dispose();
        _autoLockTimer = null;
    }

    private void OnAutoLockElapsed()
    {
        bool transitioned;
        lock (_gate)
        {
            _longUnlockActive = false;
            transitioned = LockCore();
        }

        if (transitioned)
        {
            _ = SafeRecordAsync("Door CLOSED/LOCKED", null);
        }
    }

    private async Task SafeRecordAsync(string action, string? badgeId)
    {
        try
        {
            await _actionLog.RecordAsync(action, badgeId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record door auto-transition");
        }
    }

    public void Dispose()
    {
        _autoLockTimer?.Dispose();
        _temporaryTimer?.Dispose();
    }
}
