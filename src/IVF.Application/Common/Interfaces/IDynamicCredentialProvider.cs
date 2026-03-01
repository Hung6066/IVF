using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Provides dynamic, short-lived database credentials for services.
/// Creates temporary PostgreSQL roles with auto-expiry, tracked in VaultDynamicCredential.
/// </summary>
public interface IDynamicCredentialProvider
{
    /// <summary>
    /// Generate a temporary database credential with the specified TTL.
    /// Creates a PostgreSQL role and returns the connection string.
    /// </summary>
    Task<DynamicCredentialResult> GenerateCredentialAsync(
        DynamicCredentialRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Revoke a dynamic credential and drop the associated DB role.
    /// </summary>
    Task RevokeCredentialAsync(Guid credentialId, CancellationToken ct = default);

    /// <summary>
    /// Revoke all expired credentials and drop their DB roles.
    /// </summary>
    Task<int> RevokeExpiredCredentialsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get a valid (non-expired, non-revoked) connection string for a credential.
    /// </summary>
    Task<string?> GetConnectionStringAsync(Guid credentialId, CancellationToken ct = default);
}

public record DynamicCredentialRequest(
    string DbHost,
    int DbPort,
    string DbName,
    string AdminUsername,
    string AdminPassword,
    int TtlSeconds = 3600,
    string[]? GrantedTables = null,
    bool ReadOnly = false);

public record DynamicCredentialResult(
    Guid Id,
    string LeaseId,
    string Username,
    string Password,
    string ConnectionString,
    DateTime ExpiresAt);
