using System.Device.Gpio;
using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoorApp.Familab.Infrastructure.Hardware;

/// <summary>
/// Drives the door latch relay via the Raspberry Pi GPIO using the BCM pin from
/// configuration. Ports the Python GPIO.setup(RELAY_PIN, OUT) / GPIO.output(...) logic.
/// HIGH = energised = unlocked, LOW = locked.
/// </summary>
public sealed class GpioDoorRelay : IDoorRelay
{
    private readonly ILogger<GpioDoorRelay> _logger;
    private readonly int _relayPin;
    private readonly GpioController _controller;

    public GpioDoorRelay(IOptions<DoorOptions> options, ILogger<GpioDoorRelay> logger)
    {
        _logger = logger;
        _relayPin = options.Value.Hardware.RelayPin;

        // PinNumberingScheme.Logical maps to BCM numbering on the Raspberry Pi.
        _controller = new GpioController(PinNumberingScheme.Logical);
        _controller.OpenPin(_relayPin, PinMode.Output);
        _controller.Write(_relayPin, PinValue.Low);
        _logger.LogInformation("GPIO relay initialized on BCM pin {Pin}", _relayPin);
    }

    public string BackendName => $"GPIO (BCM {_relayPin})";

    public void Energize()
    {
        _controller.Write(_relayPin, PinValue.High);
        _logger.LogDebug("Relay energized (door unlocked) on pin {Pin}", _relayPin);
    }

    public void DeEnergize()
    {
        _controller.Write(_relayPin, PinValue.Low);
        _logger.LogDebug("Relay de-energized (door locked) on pin {Pin}", _relayPin);
    }

    public void Dispose()
    {
        try
        {
            if (_controller.IsPinOpen(_relayPin))
            {
                _controller.Write(_relayPin, PinValue.Low);
                _controller.ClosePin(_relayPin);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during GPIO cleanup");
        }
        finally
        {
            _controller.Dispose();
        }
    }
}
