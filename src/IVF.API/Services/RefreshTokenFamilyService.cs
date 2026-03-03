using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace IVF.API.Services;

/// <summary>
/// Refresh Token Family Tracking (Google/Microsoft pattern).
/// 
/// Detects refresh token reuse attacks by maintaining token lineage.
/// If a previously-used token is presented, the entire family is revoked
/// (indicates the token was stolen and both attacker + victim are using it).
///
/// Based on: RFC 6749 Section 10.4 + IETF draft-ietf-oauth-security-topics
/// </summary>
public sealed class RefreshTokenFamilyService
{
    private readonly ConcurrentDictionary<string, TokenFamily> _families = new();
    private readonly ILogger<RefreshTokenFamilyService> _logger;

    public RefreshTokenFamilyService(ILogger<RefreshTokenFamilyService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register a new refresh token in a family. Call when issuing a new token pair.
    /// </summary>
    public void RegisterToken(Guid userId, string currentToken, string? previousToken)
    {
        var tokenHash = HashToken(currentToken);
        var familyId = previousToken != null
            ? GetFamilyId(HashToken(previousToken)) ?? Guid.NewGuid().ToString()
            : Guid.NewGuid().ToString();

        var family = _families.GetOrAdd(familyId, _ => new TokenFamily
        {
            FamilyId = familyId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        });

        // Mark previous token as used
        if (previousToken != null)
        {
            var prevHash = HashToken(previousToken);
            family.UsedTokens.TryAdd(prevHash, DateTime.UtcNow);
        }

        // Register current token
        family.ActiveTokenHash = tokenHash;
        family.LastRotatedAt = DateTime.UtcNow;
        _families[familyId] = family;

        // Map token hash -> family for lookup
        _families.TryAdd($"tok:{tokenHash}", family);
    }

    /// <summary>
    /// Validate a refresh token. Returns false if it was already used (reuse attack).
    /// </summary>
    public TokenFamilyValidation ValidateToken(string token)
    {
        var tokenHash = HashToken(token);
        var familyId = GetFamilyId(tokenHash);

        if (familyId == null)
        {
            return new TokenFamilyValidation(IsValid: true, IsReuse: false, FamilyId: null);
        }

        if (!_families.TryGetValue(familyId, out var family))
        {
            return new TokenFamilyValidation(IsValid: true, IsReuse: false, FamilyId: null);
        }

        // Check if this token was already used (REUSE ATTACK!)
        if (family.UsedTokens.ContainsKey(tokenHash))
        {
            _logger.LogCritical(
                "REFRESH TOKEN REUSE DETECTED! UserId={UserId}, FamilyId={FamilyId}. Revoking entire family.",
                family.UserId, family.FamilyId);

            return new TokenFamilyValidation(
                IsValid: false,
                IsReuse: true,
                FamilyId: family.FamilyId,
                UserId: family.UserId);
        }

        // Check if this is the active token
        if (family.ActiveTokenHash != tokenHash)
        {
            return new TokenFamilyValidation(IsValid: false, IsReuse: true, FamilyId: family.FamilyId, UserId: family.UserId);
        }

        return new TokenFamilyValidation(IsValid: true, IsReuse: false, FamilyId: family.FamilyId);
    }

    /// <summary>
    /// Revoke an entire token family (used when reuse is detected).
    /// </summary>
    public void RevokeFamily(string familyId)
    {
        if (_families.TryRemove(familyId, out var family))
        {
            // Clean up token->family mappings
            if (family.ActiveTokenHash != null)
                _families.TryRemove($"tok:{family.ActiveTokenHash}", out _);

            foreach (var tokenHash in family.UsedTokens.Keys)
                _families.TryRemove($"tok:{tokenHash}", out _);

            _logger.LogWarning("Revoked token family {FamilyId} for user {UserId}", familyId, family.UserId);
        }
    }

    /// <summary>
    /// Clean up expired families (call periodically).
    /// </summary>
    public void CleanupExpired(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var expired = _families.Where(kv => !kv.Key.StartsWith("tok:") && kv.Value.LastRotatedAt < cutoff)
            .Select(kv => kv.Key).ToList();

        foreach (var familyId in expired)
            RevokeFamily(familyId);
    }

    private string? GetFamilyId(string tokenHash)
    {
        if (_families.TryGetValue($"tok:{tokenHash}", out var family))
            return family.FamilyId;
        return null;
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    }

    private class TokenFamily
    {
        public string FamilyId { get; set; } = "";
        public Guid UserId { get; set; }
        public string? ActiveTokenHash { get; set; }
        public ConcurrentDictionary<string, DateTime> UsedTokens { get; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime LastRotatedAt { get; set; }
    }
}

public record TokenFamilyValidation(bool IsValid, bool IsReuse, string? FamilyId, Guid? UserId = null);
