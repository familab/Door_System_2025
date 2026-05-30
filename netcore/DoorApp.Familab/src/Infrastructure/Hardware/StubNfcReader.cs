using System.Collections.Concurrent;
using DoorApp.Familab.Domain;
using Microsoft.Extensions.Logging;

namespace DoorApp.Familab.Infrastructure.Hardware;

/// <summary>
/// In-memory NFC reader for development / CI / non-Pi hosts. Equivalent to the Python
/// PN532Stub. Tests and dev tooling can enqueue UIDs to simulate scans.
/// </summary>
public sealed class StubNfcReader : INfcReader
{
    private readonly ILogger<StubNfcReader> _logger;
    private readonly ConcurrentQueue<string> _queued = new();

    public StubNfcReader(ILogger<StubNfcReader> logger)
    {
        _logger = logger;
    }

    public string BackendName => "PN532 stub";

    public void Initialize() => _logger.LogInformation("NFC stub initialized");

    /// <summary>Simulate a card presentation (used by dev tooling and tests).</summary>
    public void EnqueueScan(string uid) => _queued.Enqueue(uid.Trim().ToLowerInvariant());

    public async Task<string?> ReadUidAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_queued.TryDequeue(out var uid))
        {
            return uid;
        }

        try
        {
            await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }

        return null;
    }

    public void Dispose()
    {
        // nothing to release
    }
}
