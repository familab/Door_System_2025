using DoorApp.Familab.Application.Abstractions;
using DoorApp.Familab.Domain;

namespace DoorApp.Familab.Application.Services;

/// <summary>
/// Thread-safe singleton holding live runtime status counters. Mirrors the global
/// state tracked in the Python server.state module (last PN532 success/error, etc.).
/// </summary>
public sealed class RuntimeStatus : IRuntimeStatus
{
    private readonly ISystemClock _clock;
    private readonly object _gate = new();

    private DateTimeOffset? _lastNfcSuccess;
    private string? _lastNfcError;
    private DateTimeOffset? _lastDataConnection;
    private DateTimeOffset? _lastBadgeRefresh;
    private string? _lastStorageError;

    public RuntimeStatus(ISystemClock clock)
    {
        _clock = clock;
        StartedAt = clock.Now;
    }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? LastNfcSuccess { get { lock (_gate) return _lastNfcSuccess; } }
    public string? LastNfcError { get { lock (_gate) return _lastNfcError; } }
    public DateTimeOffset? LastDataConnection { get { lock (_gate) return _lastDataConnection; } }
    public DateTimeOffset? LastBadgeRefresh { get { lock (_gate) return _lastBadgeRefresh; } }
    public string? LastStorageError { get { lock (_gate) return _lastStorageError; } }

    public void RecordNfcSuccess() { lock (_gate) _lastNfcSuccess = _clock.Now; }
    public void RecordNfcError(string error) { lock (_gate) _lastNfcError = error; }
    public void RecordDataConnection() { lock (_gate) _lastDataConnection = _clock.Now; }
    public void RecordBadgeRefresh() { lock (_gate) _lastBadgeRefresh = _clock.Now; }
    public void RecordStorageError(string? error) { lock (_gate) _lastStorageError = error; }
}
