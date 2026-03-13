using System.Text.Json;
using StackExchange.Redis;

namespace IVF.API.Services;

/// <summary>
/// Redis-backed MFA pending state — works across Swarm replicas.
/// Falls back to in-memory ConcurrentDictionary when Redis is unavailable.
/// Entries auto-expire after 5 minutes via Redis TTL.
/// </summary>
public sealed class MfaPendingService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<MfaPendingService> _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _fallback = new();
    private const string KeyPrefix = "mfa:pending:";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public record MfaPendingInfo(Guid UserId, string Username, string IpAddress, string? UserAgent, DateTime ExpiresAt, string MfaMethod);

    public MfaPendingService(ILogger<MfaPendingService> logger, IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
        _redis = redis;
    }

    public async Task StoreAsync(string mfaToken, MfaPendingInfo info)
    {
        var json = JsonSerializer.Serialize(info);

        if (_redis is { IsConnected: true })
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(KeyPrefix + mfaToken, json, Ttl);
        }
        else
        {
            _fallback[mfaToken] = json;
        }
    }

    public async Task<MfaPendingInfo?> GetAsync(string mfaToken)
    {
        string? json = null;

        if (_redis is { IsConnected: true })
        {
            var db = _redis.GetDatabase();
            json = await db.StringGetAsync(KeyPrefix + mfaToken);
        }
        else
        {
            _fallback.TryGetValue(mfaToken, out json);
        }

        if (string.IsNullOrEmpty(json)) return null;

        var info = JsonSerializer.Deserialize<MfaPendingInfo>(json);
        if (info is null || DateTime.UtcNow > info.ExpiresAt) return null;
        return info;
    }

    public async Task<MfaPendingInfo?> RemoveAsync(string mfaToken)
    {
        var info = await GetAsync(mfaToken);
        if (info is null) return null;

        if (_redis is { IsConnected: true })
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(KeyPrefix + mfaToken);
        }
        else
        {
            _fallback.TryRemove(mfaToken, out _);
        }

        return info;
    }
}
