namespace DoorApp.Familab.Domain.Models;

/// <summary>
/// Immutable snapshot of the door's lock state and when it last changed.
/// In the Python project this was the global _door_is_open / _door_status_updated pair.
/// </summary>
public sealed record DoorState(bool IsOpen, DateTimeOffset UpdatedAt)
{
    public string DisplayStatus => IsOpen ? "OPEN/UNLOCKED" : "CLOSED/LOCKED";

    public static DoorState Locked(DateTimeOffset at) => new(false, at);
    public static DoorState Unlocked(DateTimeOffset at) => new(true, at);
}
