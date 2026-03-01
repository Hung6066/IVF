using System.Security.Claims;
using System.Text.RegularExpressions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Evaluates vault policies for both JWT users and vault token-authenticated requests.
/// JWT users: policies loaded via VaultUserPolicy assignments.
/// Vault tokens: policies loaded from VaultToken.Policies array.
/// Admin role bypasses all policy checks.
/// </summary>
public class VaultPolicyEvaluator : IVaultPolicyEvaluator
{
    private readonly IVaultRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<VaultPolicyEvaluator> _logger;
    private readonly VaultMetrics _metrics;

    // Request-scoped cache
    private List<VaultPolicy>? _allPoliciesCache;

    public VaultPolicyEvaluator(
        IVaultRepository repo,
        ICurrentUserService currentUser,
        IHttpContextAccessor httpContextAccessor,
        ILogger<VaultPolicyEvaluator> logger,
        VaultMetrics metrics)
    {
        _repo = repo;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<PolicyEvaluation> EvaluateAsync(
        string resourcePath, string capability, CancellationToken ct = default)
    {
        var role = _currentUser.Role;

        // Admin bypass
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            _metrics.RecordPolicyEvaluation(true);
            return new PolicyEvaluation(true, "admin-bypass", "Admin role has full access");
        }

        // Check if authenticated via vault token
        var authMethod = _httpContextAccessor.HttpContext?.User
            .FindFirst("auth_method")?.Value;

        if (authMethod == "vault_token")
        {
            return await EvaluateForVaultTokenAsync(resourcePath, capability, ct);
        }

        // JWT user — check VaultUserPolicy assignments
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return new PolicyEvaluation(false, null, "User not authenticated");
        }

        return await EvaluateForUserAsync(userId.Value, resourcePath, capability, ct);
    }

    public async Task<PolicyEvaluation> EvaluateForUserAsync(
        Guid userId, string resourcePath, string capability, CancellationToken ct = default)
    {
        var userPolicies = await _repo.GetUserPoliciesByUserIdAsync(userId, ct);
        if (userPolicies.Count == 0)
        {
            return new PolicyEvaluation(false, null, "No vault policies assigned to user");
        }

        var allPolicies = await GetAllPoliciesAsync(ct);

        foreach (var up in userPolicies)
        {
            var policy = up.Policy ?? allPolicies.FirstOrDefault(p => p.Id == up.PolicyId);
            if (policy is null) continue;

            if (MatchesPolicy(policy, resourcePath, capability))
            {
                _logger.LogDebug("User {UserId} granted '{Capability}' on '{Path}' via policy '{Policy}'",
                    userId, capability, resourcePath, policy.Name);
                _metrics.RecordPolicyEvaluation(true);
                return new PolicyEvaluation(true, policy.Name, $"Granted by policy '{policy.Name}'");
            }
        }

        _metrics.RecordPolicyEvaluation(false);
        return new PolicyEvaluation(false, null,
            $"No policy grants '{capability}' on '{resourcePath}'");
    }

    public async Task<IReadOnlyList<EffectivePolicy>> GetEffectivePoliciesAsync(CancellationToken ct = default)
    {
        var role = _currentUser.Role;

        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return [new EffectivePolicy("admin", "**", ["read", "create", "update", "delete", "list", "sudo"], "admin-bypass")];
        }

        var authMethod = _httpContextAccessor.HttpContext?.User
            .FindFirst("auth_method")?.Value;

        if (authMethod == "vault_token")
        {
            return await GetEffectivePoliciesForVaultTokenAsync(ct);
        }

        var userId = _currentUser.UserId;
        if (userId is null) return [];

        return await GetEffectivePoliciesForUserAsync(userId.Value, ct);
    }

    public async Task<IReadOnlyList<EffectivePolicy>> GetEffectivePoliciesForUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        var userPolicies = await _repo.GetUserPoliciesByUserIdAsync(userId, ct);
        var allPolicies = await GetAllPoliciesAsync(ct);

        var result = new List<EffectivePolicy>();
        foreach (var up in userPolicies)
        {
            var policy = up.Policy ?? allPolicies.FirstOrDefault(p => p.Id == up.PolicyId);
            if (policy is null) continue;

            result.Add(new EffectivePolicy(
                policy.Name, policy.PathPattern, policy.Capabilities, "user-assignment"));
        }
        return result.AsReadOnly();
    }

    // ─── Private Helpers ──────────────────────────────

    private async Task<PolicyEvaluation> EvaluateForVaultTokenAsync(
        string resourcePath, string capability, CancellationToken ct)
    {
        var policyNames = _httpContextAccessor.HttpContext?.User
            .FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray() ?? [];

        if (policyNames.Length == 0)
        {
            return new PolicyEvaluation(false, null, "Vault token has no policies");
        }

        var allPolicies = await GetAllPoliciesAsync(ct);
        var tokenPolicies = allPolicies
            .Where(p => policyNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var policy in tokenPolicies)
        {
            if (MatchesPolicy(policy, resourcePath, capability))
            {
                _metrics.RecordPolicyEvaluation(true);
                return new PolicyEvaluation(true, policy.Name, $"Granted by token policy '{policy.Name}'");
            }
        }

        _metrics.RecordPolicyEvaluation(false);
        return new PolicyEvaluation(false, null,
            $"No token policy grants '{capability}' on '{resourcePath}'");
    }

    private async Task<IReadOnlyList<EffectivePolicy>> GetEffectivePoliciesForVaultTokenAsync(CancellationToken ct)
    {
        var policyNames = _httpContextAccessor.HttpContext?.User
            .FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray() ?? [];

        var allPolicies = await GetAllPoliciesAsync(ct);
        return allPolicies
            .Where(p => policyNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            .Select(p => new EffectivePolicy(p.Name, p.PathPattern, p.Capabilities, "vault-token"))
            .ToList()
            .AsReadOnly();
    }

    private async Task<List<VaultPolicy>> GetAllPoliciesAsync(CancellationToken ct)
    {
        return _allPoliciesCache ??= await _repo.GetPoliciesAsync(ct);
    }

    private static bool MatchesPolicy(VaultPolicy policy, string resourcePath, string capability)
    {
        if (!PathMatches(policy.PathPattern, resourcePath))
            return false;

        // "sudo" implies all capabilities
        if (policy.Capabilities.Contains("sudo", StringComparer.OrdinalIgnoreCase))
            return true;

        return policy.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Match vault path patterns. Supports * (single segment) and ** (any depth).
    /// </summary>
    private static bool PathMatches(string pattern, string path)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*\\*", ".*")
            .Replace("\\*", "[^/]+")
            + "$";

        return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
    }
}
