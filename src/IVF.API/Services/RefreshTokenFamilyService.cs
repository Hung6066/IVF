using System.Security.Cryptography;
using StackExchange.Redis;

namespace IVF.API.Services;

/// <summary>
/// Refresh Token Family Tracking (Google/Microsoft pattern) — Redis-backed.
/// 
/// Detects refresh token reuse attacks by maintaining token lineage in Redis,
/// shared across all API replicas in the Docker Swarm cluster.
/// If a previously-used token is presented, the entire family is revoked
/// (indicates the token was stolen and both attacker + victim are using it).
///
/// Redis keys:
///   rtf:family:{familyId}  → Hash { userId, activeToken, createdAt, lastRotatedAt }
///   rtf:used:{familyId}    → Set of used token hashes
///   rtf:tok:{tokenHash}    → String: familyId (reverse lookup)
///   TTL: 8 days (slightly longer than refresh token lifetime of 7 days)
///
/// Based on: RFC 6749 Section 10.4 + IETF draft-ietf-oauth-security-topics
/// </summary>
public sealed class RefreshTokenFamilyService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RefreshTokenFamilyService> _logger;

    private const string FamilyPrefix = "rtf:family:";
    private const string UsedPrefix = "rtf:used:";
    private const string TokenPrefix = "rtf:tok:";
    private static readonly TimeSpan FamilyTtl = TimeSpan.FromDays(8);

    public RefreshTokenFamilyService(
        ILogger<RefreshTokenFamilyService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
        _redis = redis;
    }

    private bool IsRedisAvailable => _redis is { IsConnected: true };

    /// <summary>
    /// Register a new refresh token in a family. Call when issuing a new token pair.
    /// </summary>
    public async Task RegisterTokenAsync(Guid userId, string currentToken, string? previousToken)
    {
        if (!IsRedisAvailable)
        {
            _logger.LogDebug("Redis unavailable — skipping token family registration");
            return;
        }

        try
        {
            var db = _redis!.GetDatabase();
            var tokenHash = HashToken(currentToken);

            // Determine family: continue existing or start new
            string familyId;
            if (previousToken != null)
            {
                var prevHash = HashToken(previousToken);
                var existing = await db.StringGetAsync($"{TokenPrefix}{prevHash}");
                familyId = existing.HasValue ? existing.ToString() : Guid.NewGuid().ToString();

                // Mark previous token as used
                await db.SetAddAsync($"{UsedPrefix}{familyId}", prevHash);
                await db.KeyExpireAsync($"{UsedPrefix}{familyId}", FamilyTtl);

                // Remove old token→family mapping
                await db.KeyDeleteAsync($"{TokenPrefix}{prevHash}");
            }
            else
            {
                familyId = Guid.NewGuid().ToString();
            }

            // Store/update family metadata
            var familyKey = $"{FamilyPrefix}{familyId}";
            await db.HashSetAsync(familyKey, [
                new HashEntry("userId", userId.ToString()),
                new HashEntry("activeToken", tokenHash),
                new HashEntry("createdAt", DateTime.UtcNow.Ticks.ToString()),
                new HashEntry("lastRotatedAt", DateTime.UtcNow.Ticks.ToString()),
            ]);
            await db.KeyExpireAsync(familyKey, FamilyTtl);

            // Map new token hash → family
            await db.StringSetAsync($"{TokenPrefix}{tokenHash}", familyId, FamilyTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register token family in Redis — degraded mode");
        }
    }

    /// <summary>
    /// Synchronous wrapper for backward compatibility (login, MFA, passkey flows).
    /// </summary>
    public void RegisterToken(Guid userId, string currentToken, string? previousToken)
    {
        _ = Task.Run(() => RegisterTokenAsync(userId, currentToken, previousToken));
    }

    /// <summary>
    /// Validate a refresh token. Returns false if it was already used (reuse attack).
    /// </summary>
    public async Task<TokenFamilyValidation> ValidateTokenAsync(string token)
    {
        if (!IsRedisAvailable)
        {
            // Redis down — fall through to DB-level validation (GetByRefreshTokenAsync)
            return new TokenFamilyValidation(IsValid: true, IsReuse: false, FamilyId: null);
        }

        try
        {
            var db = _redis!.GetDatabase();
            var tokenHash = HashToken(token);

            // Look up which family this token belongs to
            var familyIdValue = await db.StringGetAsync($"{TokenPrefix}{tokenHash}");
            if (!familyIdValue.HasValue)
            {
                // Unknown token — may have been registered on a previous deployment
                // Fall through to DB validation
                return new TokenFamilyValidation(IsValid: true, IsReuse: false, FamilyId: null);
            }

            var familyId = familyIdValue.ToString();
            var familyKey = $"{FamilyPrefix}{familyId}";

            // Check if this token was already used (REUSE ATTACK!)
            var wasUsed = await db.SetContainsAsync($"{UsedPrefix}{familyId}", tokenHash);
            if (wasUsed)
            {
                var userIdStr = (string?)await db.HashGetAsync(familyKey, "userId");
                Guid.TryParse(userIdStr, out var userId);

                _logger.LogCritical(
                    "REFRESH TOKEN REUSE DETECTED! UserId={UserId}, FamilyId={FamilyId}. Revoking entire family.",
                    userId, familyId);

                return new TokenFamilyValidation(
                    IsValid: false,
                    IsReuse: true,
                    FamilyId: familyId,
                    UserId: userId);
            }

            // Verify this is the active token for the family
            var activeToken = await db.HashGetAsync(familyKey, "activeToken");
            if (activeToken.HasValue && activeToken.ToString() != tokenHash)
            {
                var userIdStr = (string?)await db.HashGetAsync(familyKey, "userId");
                Guid.TryParse(userIdStr, out var userId);

                return new TokenFamilyValidation(
                    IsValid: false,
                    IsReuse: true,
                    FamilyId: familyId,
                    UserId: userId);
            }

            return new TokenFamilyValidation(IsValid: true, IsReuse: false, FamilyId: familyId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis error during token validation — falling back to DB validation");
            return new TokenFamilyValidation(IsValid: true, IsReuse: false, FamilyId: null);
        }
    }

    /// <summary>
    /// Synchronous validation wrapper (backward compat).
    /// </summary>
    public TokenFamilyValidation ValidateToken(string token)
    {
        return ValidateTokenAsync(token).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Revoke an entire token family (used when reuse is detected).
    /// </summary>
    public async Task RevokeFamilyAsync(string familyId)
    {
        if (!IsRedisAvailable) return;

        try
        {
            var db = _redis!.GetDatabase();
            var familyKey = $"{FamilyPrefix}{familyId}";

            // Get active token to clean up reverse mapping
            var activeToken = await db.HashGetAsync(familyKey, "activeToken");
            if (activeToken.HasValue)
                await db.KeyDeleteAsync($"{TokenPrefix}{activeToken}");

            // Get all used tokens to clean up reverse mappings
            var usedTokens = await db.SetMembersAsync($"{UsedPrefix}{familyId}");
            foreach (var tok in usedTokens)
                await db.KeyDeleteAsync($"{TokenPrefix}{tok}");

            // Delete family and used set
            await db.KeyDeleteAsync(familyKey);
            await db.KeyDeleteAsync($"{UsedPrefix}{familyId}");

            var userIdStr = await db.HashGetAsync(familyKey, "userId");
            _logger.LogWarning("Revoked token family {FamilyId} for user {UserId}", familyId, userIdStr);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to revoke token family {FamilyId} in Redis", familyId);
        }
    }

    /// <summary>
    /// Synchronous wrapper for backward compatibility.
    /// </summary>
    public void RevokeFamily(string familyId)
    {
        _ = Task.Run(() => RevokeFamilyAsync(familyId));
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    }
}

public record TokenFamilyValidation(bool IsValid, bool IsReuse, string? FamilyId, Guid? UserId = null);
