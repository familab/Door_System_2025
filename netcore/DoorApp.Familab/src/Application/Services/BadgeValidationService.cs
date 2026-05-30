using DoorApp.Familab.Application.Abstractions;
using DoorApp.Familab.Domain;
using DoorApp.Familab.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DoorApp.Familab.Application.Services;

/// <summary>
/// Validates a scanned badge UID against the badge store. Ports the access decision
/// logic from start.py (_check_uid_from_sources / check_local_csv).
/// </summary>
public sealed class BadgeValidationService : IBadgeValidationService
{
    private readonly IBadgeStore _store;
    private readonly IRuntimeStatus _status;
    private readonly ILogger<BadgeValidationService> _logger;

    public BadgeValidationService(IBadgeStore store, IRuntimeStatus status, ILogger<BadgeValidationService> logger)
    {
        _store = store;
        _status = status;
        _logger = logger;
    }

    public async Task<BadgeScanOutcome> ValidateAsync(string uid, CancellationToken cancellationToken = default)
    {
        var normalized = (uid ?? string.Empty).Trim().ToLowerInvariant();
        try
        {
            var granted = await _store.ContainsAsync(normalized, cancellationToken).ConfigureAwait(false);
            _status.RecordDataConnection();
            return new BadgeScanOutcome(normalized, granted, "Badge Store");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Badge lookup failed for {Uid}", normalized);
            _status.RecordStorageError(ex.Message);
            return new BadgeScanOutcome(normalized, false, "Badge Store");
        }
    }
}
