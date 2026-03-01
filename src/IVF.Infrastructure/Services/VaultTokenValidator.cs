using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Validates vault tokens by hashing the raw token and looking up the hash.
/// Evaluates token policies for path-based capability checks.
/// </summary>
public class VaultTokenValidator : IVaultTokenValidator
{
    private readonly IVaultRepository _repo;
    private readonly ILogger<VaultTokenValidator> _logger;
    private readonly VaultMetrics _metrics;

    public VaultTokenValidator(
        IVaultRepository repo,
        ILogger<VaultTokenValidator> logger,
        VaultMetrics metrics)
    {
        _repo = repo;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<VaultTokenValidationResult?> ValidateTokenAsync(
        string rawToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        var tokenHash = ComputeHash(rawToken);
        var token = await _repo.GetTokenByHashAsync(tokenHash, ct);

        if (token is null)
        {
            _logger.LogDebug("Vault token not found for hash");
            _metrics.RecordTokenValidation("not_found");
            return null;
        }

        if (!token.IsValid)
        {
            _logger.LogDebug("Vault token {Accessor} is invalid (revoked={Revoked}, expired={Expired}, exhausted={Exhausted})",
                token.Accessor, token.Revoked, token.IsExpired, token.IsExhausted);
            _metrics.RecordTokenValidation("invalid");
            return null;
        }

        // Increment usage
        token.IncrementUse();
        await _repo.UpdateTokenAsync(token, ct);

        _logger.LogDebug("Vault token {Accessor} validated (uses: {Uses})", token.Accessor, token.UsesCount);
        _metrics.RecordTokenValidation("valid");

        return new VaultTokenValidationResult(
            token.Id,
            token.Accessor,
            token.DisplayName,
            token.Policies,
            token.TokenType,
            token.ExpiresAt,
            token.UsesCount);
    }

    public async Task<bool> HasCapabilityAsync(
        string rawToken,
        string path,
        string capability,
        CancellationToken ct = default)
    {
        var tokenHash = ComputeHash(rawToken);
        var token = await _repo.GetTokenByHashAsync(tokenHash, ct);

        if (token is null || !token.IsValid)
            return false;

        // Load policies attached to this token
        var allPolicies = await _repo.GetPoliciesAsync(ct);
        var tokenPolicies = allPolicies
            .Where(p => token.Policies.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        // Check if any policy grants the requested capability on the path
        foreach (var policy in tokenPolicies)
        {
            if (PathMatches(policy.PathPattern, path) &&
                policy.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            // "sudo" capability implies all other capabilities
            if (PathMatches(policy.PathPattern, path) &&
                policy.Capabilities.Contains("sudo", StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ComputeHash(string rawToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }

    /// <summary>
    /// Match vault path patterns. Supports * (single segment) and ** (any depth).
    /// Example: "app/*/prod" matches "app/db/prod" but not "app/db/staging/prod".
    /// </summary>
    private static bool PathMatches(string pattern, string path)
    {
        // Convert vault pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*\\*", ".*")    // ** → match anything
            .Replace("\\*", "[^/]+")    // * → match single segment
            + "$";

        return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
    }
}
