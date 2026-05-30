using DoorApp.Familab.Domain.Models;

namespace DoorApp.Familab.Application.Abstractions;

/// <summary>
/// Central door controller: owns the thread-safe door state, drives the relay, and
/// manages auto-relock timers. Ports Python door_control.DoorController plus the
/// set_door_status / get_door_status module globals.
/// </summary>
public interface IDoorControlService
{
    DoorState State { get; }

    /// <summary>Unlock for a duration (defaults to configured manual unlock duration) then auto-lock.</summary>
    Task UnlockAsync(TimeSpan? duration = null, string? badgeId = null, CancellationToken cancellationToken = default);

    /// <summary>Lock the door now and cancel any pending auto-lock timer.</summary>
    Task LockAsync(string? badgeId = null, CancellationToken cancellationToken = default);

    /// <summary>Badge-triggered short unlock (configured badge duration) with auto-relock.</summary>
    Task UnlockTemporarilyAsync(TimeSpan duration, string? badgeId = null, CancellationToken cancellationToken = default);

    /// <summary>Toggle lock/unlock and return the new state token ("locked" / "unlocked").</summary>
    Task<string> ToggleAsync(string? badgeId = null, CancellationToken cancellationToken = default);
}
