using DoorApp.Familab.Domain.Models;

namespace DoorApp.Familab.Application.Abstractions;

/// <summary>Assembles the system health snapshot for the /health and /display pages.</summary>
public interface IHealthService
{
    HealthSnapshot GetSnapshot();
}

/// <summary>
/// Shared mutable status counters updated by the NFC monitor and storage layers.
/// Equivalent to the Python server.state PN532 success/error timestamps.
/// </summary>
public interface IRuntimeStatus
{
    DateTimeOffset StartedAt { get; }

    DateTimeOffset? LastNfcSuccess { get; }
    string? LastNfcError { get; }
    DateTimeOffset? LastDataConnection { get; }
    DateTimeOffset? LastBadgeRefresh { get; }
    string? LastStorageError { get; }

    void RecordNfcSuccess();
    void RecordNfcError(string error);
    void RecordDataConnection();
    void RecordBadgeRefresh();
    void RecordStorageError(string? error);
}
