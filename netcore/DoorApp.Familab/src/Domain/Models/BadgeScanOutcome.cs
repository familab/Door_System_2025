namespace DoorApp.Familab.Domain.Models;

/// <summary>Result of validating a scanned badge UID against the configured store.</summary>
public sealed record BadgeScanOutcome(string Uid, bool Granted, string Source)
{
    public AccessStatus Status => Granted ? AccessStatus.Granted : AccessStatus.Denied;
}
