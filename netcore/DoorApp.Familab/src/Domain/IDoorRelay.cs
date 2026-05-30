namespace DoorApp.Familab.Domain;

/// <summary>
/// Abstraction over the GPIO relay that physically locks/unlocks the door latch.
/// In the Python project this was a direct GPIO.output(RELAY_PIN, HIGH/LOW) call.
/// HIGH energises the relay (unlocked); LOW de-energises it (locked).
/// </summary>
public interface IDoorRelay : IDisposable
{
    /// <summary>Energise the relay -> door unlocked.</summary>
    void Energize();

    /// <summary>De-energise the relay -> door locked.</summary>
    void DeEnergize();

    /// <summary>Friendly backend name for logging.</summary>
    string BackendName { get; }
}
