using DoorApp.Familab.Domain.Models;

namespace DoorApp.Familab.Application.Abstractions;

/// <summary>
/// Records audit actions to the application log AND the access-event store.
/// This is the .NET equivalent of Python's logging_utils.record_action.
/// </summary>
public interface IActionLogService
{
    /// <summary>
    /// Record an action (e.g. "Badge Scan", "Manual Unlock", "Door OPEN/UNLOCKED").
    /// Persists a normalized <see cref="AccessEvent"/> and writes a log line.
    /// </summary>
    Task RecordAsync(string action, string? badgeId = null, AccessStatus status = AccessStatus.Success, CancellationToken cancellationToken = default);
}
