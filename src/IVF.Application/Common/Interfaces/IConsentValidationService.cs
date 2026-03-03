namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Validates user consent status for GDPR/HIPAA enforcement.
/// Used by middleware (blocking) and API endpoints (advisory).
/// </summary>
public interface IConsentValidationService
{
    /// <summary>
    /// Checks if a user has a valid (granted, not expired, not deleted) consent of the given type.
    /// </summary>
    Task<bool> HasValidConsentAsync(Guid userId, string consentType, CancellationToken ct = default);

    /// <summary>
    /// Returns the list of consent types the user is missing from the required set.
    /// </summary>
    Task<List<string>> GetMissingConsentsAsync(Guid userId, IEnumerable<string> requiredTypes, CancellationToken ct = default);
}
