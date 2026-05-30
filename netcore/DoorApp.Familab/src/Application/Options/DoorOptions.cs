namespace DoorApp.Familab.Application.Options;

/// <summary>
/// Strongly-typed configuration bound from the "Door" section of appsettings.json.
/// All hardware pin mappings and timings are configurable here, mirroring the
/// original Python project's config.py defaults.
/// </summary>
public sealed class DoorOptions
{
    public const string SectionName = "Door";

    public HardwareOptions Hardware { get; set; } = new();
    public TimingOptions Timing { get; set; } = new();
    public StorageOptions Storage { get; set; } = new();
    public HealthOptions Health { get; set; } = new();
    public AuthOptions Auth { get; set; } = new();
}

public sealed class HardwareOptions
{
    /// <summary>When false the application uses in-memory hardware stubs (for dev / CI / non-Pi hosts).</summary>
    public bool UseRealHardware { get; set; }

    /// <summary>BCM pin controlling the door relay (Python RELAY_PIN, default 17).</summary>
    public int RelayPin { get; set; } = 17;

    /// <summary>BCM pin for the physical unlock button (Python BUTTON_UNLOCK_PIN, default 27).</summary>
    public int ButtonUnlockPin { get; set; } = 27;

    /// <summary>BCM pin for the physical lock button (Python BUTTON_LOCK_PIN, default 22).</summary>
    public int ButtonLockPin { get; set; } = 22;

    /// <summary>I2C bus id the PN532 is wired to (Raspberry Pi default is bus 1 = /dev/i2c-1).</summary>
    public int I2cBusId { get; set; } = 1;

    /// <summary>PN532 I2C address (default 0x24 = 36 decimal).</summary>
    public int Pn532I2cAddress { get; set; } = 0x24;
}

public sealed class TimingOptions
{
    /// <summary>Manual unlock duration in seconds (Python UNLOCK_DURATION, default 3600).</summary>
    public int UnlockDurationSeconds { get; set; } = 3600;

    /// <summary>Badge-triggered temporary unlock duration in seconds (Python DOOR_UNLOCK_BADGE_DURATION, default 5).</summary>
    public int BadgeUnlockDurationSeconds { get; set; } = 5;

    /// <summary>Physical button debounce window in seconds (Python DEBOUNCE_TIME, default 0.5).</summary>
    public double DebounceSeconds { get; set; } = 0.5;

    /// <summary>How often the NFC reader is polled, in milliseconds.</summary>
    public int NfcPollIntervalMilliseconds { get; set; } = 100;
}

public sealed class StorageOptions
{
    /// <summary>"Sqlite" or "Json".</summary>
    public string Provider { get; set; } = "Sqlite";

    public string SqlitePath { get; set; } = "data/door.db";

    public string BadgeJsonPath { get; set; } = "data/badges.json";
}

public sealed class HealthOptions
{
    public int RefreshIntervalSeconds { get; set; } = 300;
}

public sealed class AuthOptions
{
    public string MasterUsername { get; set; } = "admin";

    /// <summary>PBKDF2 hash of the master password (see MasterPasswordHasher).</summary>
    public string MasterPasswordHash { get; set; } = string.Empty;

    public int SessionTtlHours { get; set; } = 8;

    public int FailThrottleSeconds { get; set; } = 15;

    public List<string> WhitelistEmails { get; set; } = new();

    public List<string> WhitelistDomains { get; set; } = new();

    public GoogleOptions Google { get; set; } = new();
}

public sealed class GoogleOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
