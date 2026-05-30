using DoorApp.Familab.Domain.Models;

namespace DoorApp.Familab.Application.Abstractions;

/// <summary>Validates a scanned badge UID against the configured badge store.</summary>
public interface IBadgeValidationService
{
    Task<BadgeScanOutcome> ValidateAsync(string uid, CancellationToken cancellationToken = default);
}
