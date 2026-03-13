using IVF.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace IVF.API.Extensions;

/// <summary>
/// Enterprise caching configuration extensions
/// </summary>
public static class CachingExtensions
{
    /// <summary>
    /// Adds enterprise-grade distributed caching with Redis
    /// </summary>
    public static IServiceCollection AddEnterpriseCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? configuration["Redis:ConnectionString"]
            ?? "localhost:6379";

        var redisPassword = configuration["Redis:Password"];
        var redisSsl = configuration.GetValue<bool>("Redis:Ssl", false);

        // Build connection string with options
        var connectionStringBuilder = new ConfigurationOptions
        {
            EndPoints = { redisConnectionString },
            AbortOnConnectFail = false,
            ConnectTimeout = 5000,
            SyncTimeout = 5000,
            AsyncTimeout = 5000,
            ConnectRetry = 3,
            ReconnectRetryPolicy = new ExponentialRetry(5000),
            KeepAlive = 60,
            AllowAdmin = false,
            ClientName = "IVF-API"
        };

        if (!string.IsNullOrEmpty(redisPassword))
        {
            connectionStringBuilder.Password = redisPassword;
        }

        if (redisSsl)
        {
            connectionStringBuilder.Ssl = true;
            connectionStringBuilder.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                                                   System.Security.Authentication.SslProtocols.Tls13;
        }

        // Register Redis connection multiplexer
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IConnectionMultiplexer>>();

            var multiplexer = ConnectionMultiplexer.Connect(connectionStringBuilder);

            multiplexer.ConnectionFailed += (sender, args) =>
            {
                logger.LogWarning(
                    "Redis connection failed: {EndPoint}, {FailureType}",
                    args.EndPoint,
                    args.FailureType);
            };

            multiplexer.ConnectionRestored += (sender, args) =>
            {
                logger.LogInformation(
                    "Redis connection restored: {EndPoint}",
                    args.EndPoint);
            };

            multiplexer.ErrorMessage += (sender, args) =>
            {
                logger.LogError("Redis error: {Message}", args.Message);
            };

            return multiplexer;
        });

        // Register distributed cache with Redis backend
        services.AddStackExchangeRedisCache(options =>
        {
            options.ConfigurationOptions = connectionStringBuilder;
            options.InstanceName = "ivf:";
        });

        // Register cache service
        services.AddSingleton<ICacheService, DistributedCacheService>();

        // Add memory cache for L1 caching
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1024; // Max 1024 entries
            options.CompactionPercentage = 0.2; // Remove 20% when limit reached
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
        });

        return services;
    }

    /// <summary>
    /// Adds multi-level caching (L1 Memory + L2 Redis)
    /// </summary>
    public static IServiceCollection AddMultiLevelCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add base caching
        services.AddEnterpriseCaching(configuration);

        // Add multi-level cache service
        services.AddSingleton<IMultiLevelCacheService, MultiLevelCacheService>();

        return services;
    }
}

/// <summary>
/// Multi-level cache service interface (L1 Memory + L2 Redis)
/// </summary>
public interface IMultiLevelCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? l1Expiry = null, TimeSpan? l2Expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? l1Expiry = null, TimeSpan? l2Expiry = null, CancellationToken ct = default);
}

/// <summary>
/// Multi-level cache implementation
/// L1: In-memory cache (fast, limited size)
/// L2: Redis distributed cache (shared, larger)
/// </summary>
public class MultiLevelCacheService : IMultiLevelCacheService
{
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _l1Cache;
    private readonly ICacheService _l2Cache;
    private readonly ILogger<MultiLevelCacheService> _logger;

    private static readonly TimeSpan DefaultL1Expiry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultL2Expiry = TimeSpan.FromMinutes(30);

    public MultiLevelCacheService(
        Microsoft.Extensions.Caching.Memory.IMemoryCache l1Cache,
        ICacheService l2Cache,
        ILogger<MultiLevelCacheService> logger)
    {
        _l1Cache = l1Cache;
        _l2Cache = l2Cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        // Try L1 first
        if (_l1Cache.TryGetValue(key, out object? cachedValue) && cachedValue is T l1Value)
        {
            _logger.LogDebug("L1 cache hit for {Key}", key);
            return l1Value;
        }

        // Try L2
        var l2Value = await _l2Cache.GetAsync<T>(key, ct);
        if (l2Value is not null)
        {
            _logger.LogDebug("L2 cache hit for {Key}", key);

            // Promote to L1
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = DefaultL1Expiry,
                Size = 1
            };
            _l1Cache.Set(key, l2Value, cacheEntryOptions);

            return l2Value;
        }

        _logger.LogDebug("Cache miss for {Key}", key);
        return default;
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? l1Expiry = null,
        TimeSpan? l2Expiry = null,
        CancellationToken ct = default)
    {
        // Set L1
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = l1Expiry ?? DefaultL1Expiry,
            Size = 1
        };
        _l1Cache.Set(key, value!, cacheEntryOptions);

        // Set L2
        await _l2Cache.SetAsync(key, value, l2Expiry ?? DefaultL2Expiry, ct);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _l1Cache.Remove(key);
        await _l2Cache.RemoveAsync(key, ct);
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? l1Expiry = null,
        TimeSpan? l2Expiry = null,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
        {
            return cached;
        }

        var value = await factory();

        if (value is not null)
        {
            await SetAsync(key, value, l1Expiry, l2Expiry, ct);
        }

        return value;
    }
}

/// <summary>
/// Cache invalidation events for pub/sub
/// </summary>
public interface ICacheInvalidationPublisher
{
    Task PublishInvalidationAsync(string key, CancellationToken ct = default);
    Task PublishPrefixInvalidationAsync(string prefix, CancellationToken ct = default);
}

/// <summary>
/// Redis-based cache invalidation publisher
/// </summary>
public class RedisCacheInvalidationPublisher : ICacheInvalidationPublisher
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheInvalidationPublisher> _logger;
    private const string InvalidationChannel = "ivf:cache:invalidation";

    public RedisCacheInvalidationPublisher(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheInvalidationPublisher> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task PublishInvalidationAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var subscriber = _redis.GetSubscriber();
            await subscriber.PublishAsync(
                RedisChannel.Literal(InvalidationChannel),
                $"key:{key}");

            _logger.LogDebug("Published cache invalidation for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish cache invalidation for {Key}", key);
        }
    }

    public async Task PublishPrefixInvalidationAsync(string prefix, CancellationToken ct = default)
    {
        try
        {
            var subscriber = _redis.GetSubscriber();
            await subscriber.PublishAsync(
                RedisChannel.Literal(InvalidationChannel),
                $"prefix:{prefix}");

            _logger.LogDebug("Published cache prefix invalidation for {Prefix}", prefix);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish cache prefix invalidation for {Prefix}", prefix);
        }
    }
}

/// <summary>
/// Background service that listens for cache invalidation events
/// </summary>
public class CacheInvalidationSubscriber : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _memoryCache;
    private readonly ILogger<CacheInvalidationSubscriber> _logger;
    private const string InvalidationChannel = "ivf:cache:invalidation";

    public CacheInvalidationSubscriber(
        IConnectionMultiplexer redis,
        Microsoft.Extensions.Caching.Memory.IMemoryCache memoryCache,
        ILogger<CacheInvalidationSubscriber> logger)
    {
        _redis = redis;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var subscriber = _redis.GetSubscriber();

            await subscriber.SubscribeAsync(
                RedisChannel.Literal(InvalidationChannel),
                (channel, message) =>
                {
                    try
                    {
                        var messageStr = message.ToString();

                        if (messageStr.StartsWith("key:"))
                        {
                            var key = messageStr[4..];
                            _memoryCache.Remove(key);
                            _logger.LogDebug("Invalidated L1 cache for key {Key}", key);
                        }
                        else if (messageStr.StartsWith("prefix:"))
                        {
                            var prefix = messageStr[7..];
                            // Note: MemoryCache doesn't support prefix invalidation
                            // Would need to track keys or use a different approach
                            _logger.LogDebug("Received prefix invalidation for {Prefix}", prefix);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing cache invalidation message");
                    }
                });

            _logger.LogInformation("Cache invalidation subscriber started");

            // Keep running until cancelled
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cache invalidation subscriber stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache invalidation subscriber failed");
        }
    }
}
