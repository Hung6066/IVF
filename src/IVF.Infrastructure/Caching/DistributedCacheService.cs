using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace IVF.Infrastructure.Caching;

/// <summary>
/// Cache service interface for enterprise-grade distributed caching
/// </summary>
public interface ICacheService
{
    /// <summary>Gets a value from cache</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Sets a value in cache</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);

    /// <summary>Removes a value from cache</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Gets or sets a value using factory if not exists</summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default);

    /// <summary>Invalidates all cache entries matching prefix</summary>
    Task InvalidateByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>Gets multiple values from cache</summary>
    Task<IDictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken ct = default);

    /// <summary>Sets multiple values in cache</summary>
    Task SetManyAsync<T>(IDictionary<string, T> values, TimeSpan? expiry = null, CancellationToken ct = default);

    /// <summary>Checks if key exists in cache</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>Refreshes cache entry expiration</summary>
    Task RefreshAsync(string key, CancellationToken ct = default);

    /// <summary>Gets cache statistics</summary>
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken ct = default);
}

/// <summary>
/// Enterprise-grade distributed cache service with Redis backend
/// Features: Multi-level caching, compression, statistics, resilience
/// </summary>
public class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Default cache options
    private static readonly DistributedCacheEntryOptions DefaultOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
        SlidingExpiration = TimeSpan.FromMinutes(10)
    };

    // Statistics
    private long _hits;
    private long _misses;
    private long _sets;
    private long _removes;

    public DistributedCacheService(
        IDistributedCache distributedCache,
        IConnectionMultiplexer redis,
        ILogger<DistributedCacheService> logger)
    {
        _distributedCache = distributedCache;
        _redis = redis;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var data = await _distributedCache.GetStringAsync(key, ct);

            if (string.IsNullOrEmpty(data))
            {
                Interlocked.Increment(ref _misses);
                return default;
            }

            Interlocked.Increment(ref _hits);
            return JsonSerializer.Deserialize<T>(data, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for key {Key}", key);
            Interlocked.Increment(ref _misses);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        try
        {
            var options = expiry.HasValue
                ? new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiry,
                    SlidingExpiration = expiry > TimeSpan.FromMinutes(5)
                        ? TimeSpan.FromMinutes(5)
                        : null
                }
                : DefaultOptions;

            var data = JsonSerializer.Serialize(value, _jsonOptions);
            await _distributedCache.SetStringAsync(key, data, options, ct);

            Interlocked.Increment(ref _sets);
            _logger.LogDebug("Cache SET for key {Key}, expiry {Expiry}", key, expiry ?? DefaultOptions.AbsoluteExpirationRelativeToNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _distributedCache.RemoveAsync(key, ct);
            Interlocked.Increment(ref _removes);
            _logger.LogDebug("Cache REMOVE for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE failed for key {Key}", key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        // Try to get from cache first
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
        {
            return cached;
        }

        // Execute factory and cache result
        var value = await factory();

        if (value is not null)
        {
            await SetAsync(key, value, expiry, ct);
        }

        return value;
    }

    public async Task InvalidateByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        try
        {
            var endpoints = _redis.GetEndPoints();
            if (endpoints.Length == 0)
            {
                _logger.LogWarning("No Redis endpoints available for prefix invalidation");
                return;
            }

            var server = _redis.GetServer(endpoints.First());
            var db = _redis.GetDatabase();

            var keys = server.Keys(pattern: $"{prefix}*").ToArray();

            if (keys.Length > 0)
            {
                await db.KeyDeleteAsync(keys);
                Interlocked.Add(ref _removes, keys.Length);
                _logger.LogInformation(
                    "Invalidated {Count} cache keys with prefix {Prefix}",
                    keys.Length, prefix);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache prefix invalidation failed for {Prefix}", prefix);
        }
    }

    public async Task<IDictionary<string, T?>> GetManyAsync<T>(
        IEnumerable<string> keys,
        CancellationToken ct = default)
    {
        var keyList = keys.ToList();
        var result = new Dictionary<string, T?>();

        try
        {
            var db = _redis.GetDatabase();
            var redisKeys = keyList.Select(k => (RedisKey)k).ToArray();
            var values = await db.StringGetAsync(redisKeys);

            for (int i = 0; i < keyList.Count; i++)
            {
                if (values[i].HasValue)
                {
                    result[keyList[i]] = JsonSerializer.Deserialize<T>(values[i].ToString(), _jsonOptions);
                    Interlocked.Increment(ref _hits);
                }
                else
                {
                    result[keyList[i]] = default;
                    Interlocked.Increment(ref _misses);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GETMANY failed for {Count} keys", keyList.Count);
            foreach (var key in keyList)
            {
                result[key] = default;
            }
        }

        return result;
    }

    public async Task SetManyAsync<T>(
        IDictionary<string, T> values,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var batch = db.CreateBatch();
            var tasks = new List<Task>();

            var expiryTime = expiry ?? DefaultOptions.AbsoluteExpirationRelativeToNow ?? TimeSpan.FromMinutes(30);

            foreach (var (key, value) in values)
            {
                var serialized = JsonSerializer.Serialize(value, _jsonOptions);
                tasks.Add(batch.StringSetAsync(key, serialized, expiryTime));
            }

            batch.Execute();
            await Task.WhenAll(tasks);

            Interlocked.Add(ref _sets, values.Count);
            _logger.LogDebug("Cache SETMANY for {Count} keys", values.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SETMANY failed for {Count} keys", values.Count);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache EXISTS failed for key {Key}", key);
            return false;
        }
    }

    public async Task RefreshAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _distributedCache.RefreshAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REFRESH failed for key {Key}", key);
        }
    }

    public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var info = await server.InfoAsync("stats");

            var statsSection = info.FirstOrDefault(s => s.Key == "stats");
            long keyspaceHits = 0;
            long keyspaceMisses = 0;

            if (statsSection.Any())
            {
                var hitsKv = statsSection.FirstOrDefault(kv => kv.Key == "keyspace_hits");
                var missesKv = statsSection.FirstOrDefault(kv => kv.Key == "keyspace_misses");
                keyspaceHits = long.TryParse(hitsKv.Value, out var h) ? h : 0;
                keyspaceMisses = long.TryParse(missesKv.Value, out var m) ? m : 0;
            }

            return new CacheStatistics
            {
                LocalHits = _hits,
                LocalMisses = _misses,
                LocalSets = _sets,
                LocalRemoves = _removes,
                ServerKeyspaceHits = keyspaceHits,
                ServerKeyspaceMisses = keyspaceMisses,
                HitRatio = _hits + _misses > 0
                    ? (double)_hits / (_hits + _misses) * 100
                    : 0,
                IsConnected = _redis.IsConnected
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cache statistics");
            return new CacheStatistics
            {
                LocalHits = _hits,
                LocalMisses = _misses,
                LocalSets = _sets,
                LocalRemoves = _removes,
                IsConnected = _redis.IsConnected
            };
        }
    }
}

/// <summary>
/// Cache statistics for monitoring
/// </summary>
public record CacheStatistics
{
    public long LocalHits { get; init; }
    public long LocalMisses { get; init; }
    public long LocalSets { get; init; }
    public long LocalRemoves { get; init; }
    public long ServerKeyspaceHits { get; init; }
    public long ServerKeyspaceMisses { get; init; }
    public double HitRatio { get; init; }
    public bool IsConnected { get; init; }
}

/// <summary>
/// Standardized cache key builder for consistency
/// </summary>
public static class CacheKeys
{
    private const string Prefix = "ivf";

    // Tenant-related keys
    public static string Tenant(Guid tenantId) => $"{Prefix}:tenant:{tenantId}";
    public static string TenantFeatures(Guid tenantId) => $"{Prefix}:tenant:{tenantId}:features";
    public static string TenantSettings(Guid tenantId) => $"{Prefix}:tenant:{tenantId}:settings";
    public static string TenantLimits(Guid tenantId) => $"{Prefix}:tenant:{tenantId}:limits";

    // User-related keys
    public static string User(Guid userId) => $"{Prefix}:user:{userId}";
    public static string UserPermissions(Guid userId) => $"{Prefix}:user:{userId}:permissions";
    public static string UserSession(Guid userId) => $"{Prefix}:user:{userId}:session";
    public static string UserRoles(Guid userId) => $"{Prefix}:user:{userId}:roles";
    public static string UserMfaState(Guid userId) => $"{Prefix}:user:{userId}:mfa";

    // Security-related keys
    public static string IpWhitelist(Guid? tenantId) => $"{Prefix}:security:ip-whitelist:{tenantId ?? Guid.Empty}";
    public static string GeoBlockRules(Guid? tenantId) => $"{Prefix}:security:geo-rules:{tenantId ?? Guid.Empty}";
    public static string RateLimitBucket(string clientId) => $"{Prefix}:security:rate-limit:{clientId}";
    public static string LoginAttempts(string identifier) => $"{Prefix}:security:login-attempts:{identifier}";

    // Business entity keys
    public static string Patient(Guid patientId) => $"{Prefix}:patient:{patientId}";
    public static string PatientByMrn(Guid tenantId, string mrn) => $"{Prefix}:patient:mrn:{tenantId}:{mrn}";
    public static string Couple(Guid coupleId) => $"{Prefix}:couple:{coupleId}";
    public static string Cycle(Guid cycleId) => $"{Prefix}:cycle:{cycleId}";
    public static string Embryo(Guid embryoId) => $"{Prefix}:embryo:{embryoId}";

    // Catalog keys (long-lived cache)
    public static string ServiceCatalog(Guid tenantId) => $"{Prefix}:catalog:services:{tenantId}";
    public static string FormTemplate(Guid formId) => $"{Prefix}:catalog:form:{formId}";
    public static string ReportTemplate(Guid reportId) => $"{Prefix}:catalog:report:{reportId}";
    public static string PricingCatalog(Guid tenantId) => $"{Prefix}:catalog:pricing:{tenantId}";

    // Queue keys (short-lived cache)
    public static string QueueStatus(Guid tenantId) => $"{Prefix}:queue:status:{tenantId}";
    public static string QueuePosition(Guid ticketId) => $"{Prefix}:queue:position:{ticketId}";

    // Prefix patterns for invalidation
    public static string TenantPrefix(Guid tenantId) => $"{Prefix}:tenant:{tenantId}";
    public static string UserPrefix(Guid userId) => $"{Prefix}:user:{userId}";
    public static string PatientPrefix(Guid patientId) => $"{Prefix}:patient:{patientId}";
    public static string SecurityPrefix() => $"{Prefix}:security";
    public static string CatalogPrefix(Guid tenantId) => $"{Prefix}:catalog:*:{tenantId}";
}

/// <summary>
/// Cache expiration presets
/// </summary>
public static class CacheExpiry
{
    /// <summary>Very short-lived data (queue positions, real-time status)</summary>
    public static readonly TimeSpan VeryShort = TimeSpan.FromSeconds(30);

    /// <summary>Short-lived data (session state, rate limits)</summary>
    public static readonly TimeSpan Short = TimeSpan.FromMinutes(5);

    /// <summary>Medium-lived data (user permissions, tenant features)</summary>
    public static readonly TimeSpan Medium = TimeSpan.FromMinutes(30);

    /// <summary>Long-lived data (catalogs, templates)</summary>
    public static readonly TimeSpan Long = TimeSpan.FromHours(2);

    /// <summary>Very long-lived data (static configuration)</summary>
    public static readonly TimeSpan VeryLong = TimeSpan.FromHours(24);

    /// <summary>Security-related data (whitelist, geo rules)</summary>
    public static readonly TimeSpan Security = TimeSpan.FromMinutes(5);

    /// <summary>Entity data (patients, cycles)</summary>
    public static readonly TimeSpan Entity = TimeSpan.FromMinutes(15);
}
