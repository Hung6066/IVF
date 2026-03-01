using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Evaluates vault policies for users and tokens.
/// Supports both JWT-authenticated users (via VaultUserPolicy assignments)
/// and vault token-authenticated requests (via VaultToken.Policies).
/// </summary>
public interface IVaultPolicyEvaluator
{
    /// <summary>
    /// Check if the current user (JWT or vault token) has a specific capability on a resource path.
    /// </summary>
    Task<PolicyEvaluation> EvaluateAsync(string resourcePath, string capability, CancellationToken ct = default);

    /// <summary>
    /// Check if a specific user (by ID) has a capability based on their assigned vault policies.
    /// </summary>
    Task<PolicyEvaluation> EvaluateForUserAsync(Guid userId, string resourcePath, string capability, CancellationToken ct = default);

    /// <summary>
    /// Get all effective policies for the current user.
    /// </summary>
    Task<IReadOnlyList<EffectivePolicy>> GetEffectivePoliciesAsync(CancellationToken ct = default);

    /// <summary>
    /// Get all effective policies for a specific user.
    /// </summary>
    Task<IReadOnlyList<EffectivePolicy>> GetEffectivePoliciesForUserAsync(Guid userId, CancellationToken ct = default);
}

public record PolicyEvaluation(
    bool Allowed,
    string? MatchedPolicy,
    string? Reason);

public record EffectivePolicy(
    string PolicyName,
    string PathPattern,
    string[] Capabilities,
    string Source); // "user-assignment" | "vault-token" | "admin-bypass"
