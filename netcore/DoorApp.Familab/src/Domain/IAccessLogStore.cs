using DoorApp.Familab.Domain.Models;

namespace DoorApp.Familab.Domain;

/// <summary>
/// Persistence for audit/access events used by analytics. Replaces the Python
/// monthly SQLite metrics database (metrics_storage.py).
/// </summary>
public interface IAccessLogStore
{
    /// <summary>Append a single normalized event.</summary>
    Task AppendAsync(AccessEvent accessEvent, CancellationToken cancellationToken = default);

    /// <summary>Query events in the inclusive timestamp range, ordered ascending by time.</summary>
    Task<IReadOnlyList<AccessEvent>> QueryRangeAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyCollection<string>? eventTypes = null,
        CancellationToken cancellationToken = default);
}
