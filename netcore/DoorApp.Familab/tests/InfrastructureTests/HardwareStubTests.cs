using DoorApp.Familab.Infrastructure.Hardware;
using DoorApp.Familab.Infrastructure.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoorApp.Familab.Tests.InfrastructureTests;

/// <summary>Integration tests for the mock hardware (no Raspberry Pi required).</summary>
public class HardwareStubTests
{
    [Fact]
    public async Task StubNfcReader_returns_enqueued_uid_then_null()
    {
        var reader = new StubNfcReader(NullLogger<StubNfcReader>.Instance);
        reader.Initialize();
        reader.EnqueueScan("DEADBEEF");

        var first = await reader.ReadUidAsync(TimeSpan.FromMilliseconds(10));
        Assert.Equal("deadbeef", first);

        var second = await reader.ReadUidAsync(TimeSpan.FromMilliseconds(10));
        Assert.Null(second);
    }

    [Fact]
    public void StubDoorRelay_tracks_energized_state()
    {
        var relay = new StubDoorRelay(NullLogger<StubDoorRelay>.Instance);
        Assert.False(relay.Energized);
        relay.Energize();
        Assert.True(relay.Energized);
        relay.DeEnergize();
        Assert.False(relay.Energized);
    }

    [Fact]
    public void VersionProvider_reads_embedded_assembly_version()
    {
        var provider = new AssemblyVersionProvider();
        Assert.False(string.IsNullOrWhiteSpace(provider.Version));
        // Embedded AssemblyInfo ships 1.0.0; CI overwrites this.
        Assert.Matches(@"^\d+\.\d+\.\d+", provider.Version);
    }
}
