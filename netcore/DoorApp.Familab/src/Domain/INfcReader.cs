namespace DoorApp.Familab.Domain;

/// <summary>
/// Abstraction over the PN532 NFC/RFID reader. The RaspberryPi implementation talks
/// to the hardware over I2C; a stub implementation is used for development.
/// This is the .NET equivalent of the Python <c>pn532.read_passive_target()</c> call.
/// </summary>
public interface INfcReader : IDisposable
{
    /// <summary>Performs any required one-time reader configuration (SAM configuration on PN532).</summary>
    void Initialize();

    /// <summary>
    /// Polls for a passive NFC target and returns its UID as a lowercase hex string,
    /// or <c>null</c> if no card was present within <paramref name="timeout"/>.
    /// </summary>
    Task<string?> ReadUidAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>Friendly backend name for logging (e.g. "PN532 (I2C)" or "stub").</summary>
    string BackendName { get; }
}
