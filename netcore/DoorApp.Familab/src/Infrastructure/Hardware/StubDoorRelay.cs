using DoorApp.Familab.Domain;
using Microsoft.Extensions.Logging;

namespace DoorApp.Familab.Infrastructure.Hardware;

/// <summary>In-memory relay for development. Equivalent to the Python GPIO stub's output().</summary>
public sealed class StubDoorRelay : IDoorRelay
{
    private readonly ILogger<StubDoorRelay> _logger;

    public StubDoorRelay(ILogger<StubDoorRelay> logger)
    {
        _logger = logger;
    }

    /// <summary>Current energised state (exposed for tests/dev).</summary>
    public bool Energized { get; private set; }

    public string BackendName => "GPIO stub";

    public void Energize()
    {
        Energized = true;
        _logger.LogDebug("Relay energized (door unlocked) [stub]");
    }

    public void DeEnergize()
    {
        Energized = false;
        _logger.LogDebug("Relay de-energized (door locked) [stub]");
    }

    public void Dispose()
    {
        // nothing to release
    }
}
