namespace DoorApp.Familab.Domain.Models;

/// <summary>Aggregated analytics over a date range (badge scans, failures, door cycles, uptime).</summary>
public sealed record AnalyticsSummary
{
    public required DateTimeOffset RangeStart { get; init; }
    public required DateTimeOffset RangeEnd { get; init; }

    public int TotalScans { get; init; }
    public int GrantedScans { get; init; }
    public int DeniedScans { get; init; }
    public int DoorOpenCount { get; init; }
    public int ManualActions { get; init; }
    public int ErrorCount { get; init; }

    public long UptimeSeconds { get; init; }

    /// <summary>Scans per calendar day (yyyy-MM-dd -> count), ordered.</summary>
    public IReadOnlyList<DailyCount> ScansPerDay { get; init; } = Array.Empty<DailyCount>();

    /// <summary>Top badge UIDs by granted scans.</summary>
    public IReadOnlyList<BadgeCount> TopBadges { get; init; } = Array.Empty<BadgeCount>();
}

public sealed record DailyCount(string Day, int Count);

public sealed record BadgeCount(string BadgeId, int Count);
