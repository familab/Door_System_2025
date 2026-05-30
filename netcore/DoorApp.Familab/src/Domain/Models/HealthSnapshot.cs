namespace DoorApp.Familab.Domain.Models;

/// <summary>
/// Everything the public /health page and admin dashboard report about the system.
/// Equivalent to the values assembled in the Python routes_public/routes_admin handlers.
/// </summary>
public sealed record HealthSnapshot
{
    public required string Version { get; init; }
    public required DateTimeOffset Now { get; init; }
    public required string MachineName { get; init; }
    public required IReadOnlyList<string> LocalIps { get; init; }

    public required DoorState Door { get; init; }
    public required string Uptime { get; init; }
    public required long UptimeSeconds { get; init; }

    public DateTimeOffset? LastNfcSuccess { get; init; }
    public string? LastNfcError { get; init; }

    public DateTimeOffset? LastDataConnection { get; init; }
    public DateTimeOffset? LastBadgeRefresh { get; init; }
    public string? LastStorageError { get; init; }

    public required DiskSpace Disk { get; init; }
    public int RefreshIntervalSeconds { get; init; }
}

public sealed record DiskSpace(double FreeMb, double TotalMb, double UsedMb, double PercentUsed);
