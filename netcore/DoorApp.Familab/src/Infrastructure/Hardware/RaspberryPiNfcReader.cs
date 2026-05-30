using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoorApp.Familab.Infrastructure.Hardware;

/// <summary>
/// Raspberry Pi PN532 NFC reader implementation over I2C. Wraps <see cref="Pn532I2c"/>
/// and exposes UIDs as lowercase hex strings, matching the Python reader output.
/// </summary>
public sealed class RaspberryPiNfcReader : INfcReader
{
    private readonly ILogger<RaspberryPiNfcReader> _logger;
    private readonly int _busId;
    private readonly int _address;
    private Pn532I2c? _pn532;

    public RaspberryPiNfcReader(IOptions<DoorOptions> options, ILogger<RaspberryPiNfcReader> logger)
    {
        _logger = logger;
        _busId = options.Value.Hardware.I2cBusId;
        _address = options.Value.Hardware.Pn532I2cAddress;
    }

    public string BackendName => $"PN532 (I2C bus {_busId})";

    public void Initialize()
    {
        _pn532 = new Pn532I2c(_busId, _address);
        if (_pn532.SamConfiguration())
        {
            _logger.LogInformation("PN532 RFID reader initialized on I2C bus {Bus}", _busId);
        }
        else
        {
            _logger.LogWarning("PN532 SAM configuration did not acknowledge; reads may fail");
        }
    }

    public Task<string?> ReadUidAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_pn532 is null)
        {
            Initialize();
        }

        var uid = _pn532!.ReadPassiveTargetUid(timeout);
        if (uid is null || uid.Length == 0)
        {
            return Task.FromResult<string?>(null);
        }

        var hex = Convert.ToHexString(uid).ToLowerInvariant();
        return Task.FromResult<string?>(hex);
    }

    public void Dispose() => _pn532?.Dispose();
}
