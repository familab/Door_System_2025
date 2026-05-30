using DoorApp.Familab.Domain.Models;

namespace DoorApp.Familab.Application.Abstractions;

/// <summary>Aggregates access events into analytics (badge scans, failures, uptime, door cycles).</summary>
public interface IAnalyticsService
{
    Task<AnalyticsSummary> SummariseAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);

    /// <summary>Raw events in range (used by the analytics page / CSV export).</summary>
    Task<IReadOnlyList<AccessEvent>> GetEventsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
}
