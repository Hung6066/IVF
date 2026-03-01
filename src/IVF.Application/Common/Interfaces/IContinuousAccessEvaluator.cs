using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Google CAE-style continuous access evaluation.
/// Re-evaluates access during long operations, binds sessions to vault tokens,
/// and triggers step-up authentication for high-sensitivity operations.
/// </summary>
public interface IContinuousAccessEvaluator
{
    /// <summary>
    /// Evaluate whether a session is still valid for continued access.
    /// Called periodically during long-running operations.
    /// </summary>
    Task<CaeDecision> EvaluateSessionAsync(CaeSessionContext session, CancellationToken ct = default);

    /// <summary>
    /// Check if an operation requires step-up authentication (e.g., MFA).
    /// </summary>
    Task<StepUpRequirement> CheckStepUpRequirementAsync(
        ZTVaultAction action, string userId, AuthLevel currentLevel, CancellationToken ct = default);

    /// <summary>
    /// Bind a vault token to a user session for session-token correlation.
    /// </summary>
    Task BindSessionTokenAsync(string sessionId, Guid vaultTokenId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Revoke all vault tokens bound to a session (called on session anomaly or logout).
    /// </summary>
    Task<int> RevokeSessionTokensAsync(string sessionId, string reason, CancellationToken ct = default);
}

public record CaeSessionContext(
    string SessionId,
    string UserId,
    string DeviceId,
    string IpAddress,
    string? Country,
    AuthLevel CurrentAuthLevel,
    DateTime SessionStartedAt,
    DateTime? LastEvaluatedAt,
    bool IpChanged,
    bool CountryChanged);

public record CaeDecision(
    bool ContinueAccess,
    string Reason,
    bool RequiresReAuth,
    AuthLevel? RequiredAuthLevel,
    DateTime EvaluatedAt);

public record StepUpRequirement(
    bool Required,
    AuthLevel RequiredLevel,
    string Reason,
    int TimeoutSeconds);
