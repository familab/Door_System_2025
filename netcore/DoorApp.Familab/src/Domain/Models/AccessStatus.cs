namespace DoorApp.Familab.Domain.Models;

/// <summary>
/// Outcome of an access decision for a badge scan or manual action.
/// Mirrors the "Granted" / "Denied" / "Success" strings logged by the Python app.
/// </summary>
public enum AccessStatus
{
    Granted,
    Denied,
    Success,
    Failure
}

public static class AccessStatusExtensions
{
    /// <summary>The exact lowercase/string token used when persisting events (matches Python log format).</summary>
    public static string ToToken(this AccessStatus status) => status switch
    {
        AccessStatus.Granted => "Granted",
        AccessStatus.Denied => "Denied",
        AccessStatus.Success => "Success",
        AccessStatus.Failure => "Failure",
        _ => "Unknown"
    };
}
