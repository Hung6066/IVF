namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Manages vault leases programmatically. Used by business logic to create,
/// renew, and revoke leases attached to secrets.
/// </summary>
public interface ILeaseManager
{
    /// <summary>
    /// Create a lease for a secret. Returns the lease ID and expiry.
    /// The secret value is only accessible while the lease is active.
    /// </summary>
    Task<LeaseInfo> CreateLeaseAsync(string secretPath, int ttlSeconds, bool renewable = true, CancellationToken ct = default);

    /// <summary>
    /// Renew an existing lease by extending its TTL.
    /// </summary>
    Task<LeaseInfo> RenewLeaseAsync(string leaseId, int incrementSeconds, CancellationToken ct = default);

    /// <summary>
    /// Revoke a lease immediately. The associated secret remains but the lease is marked as revoked.
    /// </summary>
    Task RevokeLeaseAsync(string leaseId, CancellationToken ct = default);

    /// <summary>
    /// Get a secret value only if its lease is still active.
    /// Returns null if the lease is expired or revoked.
    /// </summary>
    Task<LeasedSecretResult?> GetLeasedSecretAsync(string leaseId, CancellationToken ct = default);

    /// <summary>
    /// List all active (non-expired, non-revoked) leases.
    /// </summary>
    Task<IReadOnlyList<LeaseInfo>> GetActiveLeasesAsync(CancellationToken ct = default);
}

public record LeaseInfo(
    string LeaseId,
    Guid SecretId,
    string? SecretPath,
    int Ttl,
    bool Renewable,
    DateTime ExpiresAt);

public record LeasedSecretResult(
    string LeaseId,
    string SecretPath,
    string Value,
    DateTime LeaseExpiresAt,
    int RemainingSeconds);
