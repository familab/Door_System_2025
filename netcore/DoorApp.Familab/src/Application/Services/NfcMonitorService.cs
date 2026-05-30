using DoorApp.Familab.Application.Abstractions;
using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Domain;
using DoorApp.Familab.Domain.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoorApp.Familab.Application.Services;

/// <summary>
/// Background loop that polls the NFC reader and authorises badges. Direct port of
/// the Python check_rfid() worker thread, running as an ASP.NET Core hosted service.
/// </summary>
public sealed class NfcMonitorService : BackgroundService
{
    private readonly INfcReader _reader;
    private readonly IBadgeValidationService _validation;
    private readonly IDoorControlService _door;
    private readonly IActionLogService _actionLog;
    private readonly IRuntimeStatus _status;
    private readonly IOptionsMonitor<DoorOptions> _options;
    private readonly ILogger<NfcMonitorService> _logger;

    public NfcMonitorService(
        INfcReader reader,
        IBadgeValidationService validation,
        IDoorControlService door,
        IActionLogService actionLog,
        IRuntimeStatus status,
        IOptionsMonitor<DoorOptions> options,
        ILogger<NfcMonitorService> logger)
    {
        _reader = reader;
        _validation = validation;
        _door = door;
        _actionLog = actionLog;
        _status = status;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RFID monitoring started (backend: {Backend})", _reader.BackendName);
        try
        {
            _reader.Initialize();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NFC reader initialization failed; continuing");
        }

        var pollTimeout = TimeSpan.FromMilliseconds(Math.Max(50, _options.CurrentValue.Timing.NfcPollIntervalMilliseconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var uid = await _reader.ReadUidAsync(pollTimeout, stoppingToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(uid))
                {
                    await Task.Delay(pollTimeout, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                _logger.LogInformation("Card scanned with UID: {Uid}", uid);
                _status.RecordNfcSuccess();

                var outcome = await _validation.ValidateAsync(uid, stoppingToken).ConfigureAwait(false);
                if (outcome.Granted)
                {
                    _logger.LogInformation("Access GRANTED for {Uid} from {Source}", uid, outcome.Source);
                    await _actionLog.RecordAsync("Badge Scan", uid, AccessStatus.Granted, stoppingToken).ConfigureAwait(false);

                    if (!_door.State.IsOpen)
                    {
                        var badgeDuration = TimeSpan.FromSeconds(_options.CurrentValue.Timing.BadgeUnlockDurationSeconds);
                        await _door.UnlockTemporarilyAsync(badgeDuration, uid, stoppingToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    _logger.LogWarning("Access DENIED for {Uid}", uid);
                    await _actionLog.RecordAsync("Badge Scan", uid, AccessStatus.Denied, stoppingToken).ConfigureAwait(false);
                }

                // Debounce repeated reads of the same card (Python waits 1s).
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NFC error in main loop");
                _status.RecordNfcError(ex.Message);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("RFID monitoring stopped");
    }
}
