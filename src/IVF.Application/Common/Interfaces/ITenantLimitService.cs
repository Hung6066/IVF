namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Service to enforce tenant resource limits and feature gating.
/// </summary>
public interface ITenantLimitService
{
    /// <summary>Throws TenantLimitExceededException if user count >= maxUsers.</summary>
    Task EnsureUserLimitAsync(CancellationToken ct = default);

    /// <summary>Throws TenantLimitExceededException if patient count >= maxPatientsPerMonth.</summary>
    Task EnsurePatientLimitAsync(CancellationToken ct = default);

    /// <summary>Throws TenantLimitExceededException if storage would exceed limit.</summary>
    Task EnsureStorageLimitAsync(long additionalBytes, CancellationToken ct = default);

    /// <summary>Throws FeatureNotEnabledException if feature is not enabled for tenant.</summary>
    Task EnsureFeatureEnabledAsync(string featureCode, CancellationToken ct = default);
}
