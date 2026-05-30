namespace DoorApp.Familab.Domain.Models;

/// <summary>
/// A normalized access/audit event. This is the .NET equivalent of the rows the
/// Python project stored in its monthly SQLite metrics database (events table).
/// </summary>
public sealed record AccessEvent
{
    public long Id { get; init; }

    /// <summary>Timestamp the event occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Normalized event token: scan, open, close, manual_unlock, manual_lock, ...</summary>
    public required string EventType { get; init; }

    /// <summary>Associated badge UID or audit identity (may be null).</summary>
    public string? BadgeId { get; init; }

    /// <summary>Normalized status token (granted, denied, success, ...).</summary>
    public required string Status { get; init; }

    /// <summary>Original human readable message.</summary>
    public string RawMessage { get; init; } = string.Empty;
}
