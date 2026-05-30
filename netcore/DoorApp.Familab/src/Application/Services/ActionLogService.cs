using DoorApp.Familab.Application.Abstractions;
using DoorApp.Familab.Domain;
using DoorApp.Familab.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DoorApp.Familab.Application.Services;

/// <summary>
/// Ports Python logging_utils.record_action: classifies severity from the status,
/// writes a structured log line, and persists a normalized event for analytics.
/// </summary>
public sealed class ActionLogService : IActionLogService
{
    private readonly IAccessLogStore _store;
    private readonly ISystemClock _clock;
    private readonly ILogger<ActionLogService> _logger;

    public ActionLogService(IAccessLogStore store, ISystemClock clock, ILogger<ActionLogService> logger)
    {
        _store = store;
        _clock = clock;
        _logger = logger;
    }

    public async Task RecordAsync(string action, string? badgeId = null, AccessStatus status = AccessStatus.Success, CancellationToken cancellationToken = default)
    {
        var statusToken = status.ToToken();
        var message = badgeId is not null
            ? $"{action} - Badge: {badgeId} - Status: {statusToken}"
            : $"{action} - Status: {statusToken}";

        switch (status)
        {
            case AccessStatus.Granted:
            case AccessStatus.Success:
                _logger.LogInformation("{Message}", message);
                break;
            case AccessStatus.Denied:
                _logger.LogWarning("{Message}", message);
                break;
            default:
                _logger.LogError("{Message}", message);
                break;
        }

        var accessEvent = new AccessEvent
        {
            Timestamp = _clock.Now,
            EventType = EventTypeNormalizer.Normalize(action),
            BadgeId = string.IsNullOrWhiteSpace(badgeId) ? null : badgeId,
            Status = statusToken.ToLowerInvariant(),
            RawMessage = message
        };

        try
        {
            await _store.AppendAsync(accessEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Auditing must never break the main flow (matches Python best-effort logging).
            _logger.LogError(ex, "Failed to persist access event");
        }
    }
}
