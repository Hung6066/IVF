namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Google-style dual-credential rotation for the application's main DB connection.
/// Maintains two active credentials (A/B), rotates alternately for zero-downtime.
/// The "live" credential serves traffic while the "standby" is being rotated.
/// </summary>
public interface IDbCredentialRotationService
{
    /// <summary>
    /// Rotate the standby DB credential. Creates a new PostgreSQL role,
    /// swaps it to active, and revokes the old one after a grace period.
    /// </summary>
    Task<DbCredentialRotationResult> RotateAsync(CancellationToken ct = default);

    /// <summary>Get current dual-credential status (active slot, versions, last rotation).</summary>
    Task<DualCredentialStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Get the currently active connection string for application use.</summary>
    Task<string?> GetActiveConnectionStringAsync(CancellationToken ct = default);
}

public record DbCredentialRotationResult(
    bool Success,
    string ActiveSlot,
    string? NewUsername,
    DateTime? ExpiresAt,
    string? Error);

public record DualCredentialStatus(
    string ActiveSlot,
    string? SlotAUsername,
    DateTime? SlotAExpiresAt,
    bool SlotAActive,
    string? SlotBUsername,
    DateTime? SlotBExpiresAt,
    bool SlotBActive,
    DateTime? LastRotatedAt,
    int RotationCount);
