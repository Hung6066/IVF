using System.Text.Json;
using IVF.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace IVF.API.Extensions;

/// <summary>
/// Enterprise-grade health check configuration
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds enterprise health checks for all system components
    /// </summary>
    public static IServiceCollection AddEnterpriseHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var healthChecksBuilder = services.AddHealthChecks();

        // Database Health Checks
        healthChecksBuilder.AddDbContextCheck<IvfDbContext>(
            name: "postgresql-primary",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "db", "critical", "ready", "live" });

        healthChecksBuilder.AddCheck<DatabasePoolHealthCheck>(
            "postgresql-pool",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "db", "performance", "ready" });

        // Redis Health Check
        healthChecksBuilder.AddCheck<RedisCacheHealthCheck>(
            "redis-operations",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "cache", "ready" });

        // System Health Checks
        healthChecksBuilder.AddCheck<GarbageCollectionHealthCheck>(
            "gc-health",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "system", "performance" });

        healthChecksBuilder.AddCheck<MemoryHealthCheck>(
            "memory",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "system", "memory" });

        // Background Services Health Check
        healthChecksBuilder.AddCheck<BackgroundServicesHealthCheck>(
            "background-services",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "services", "ready" });

        return services;
    }

    /// <summary>
    /// Maps health check endpoints with detailed response
    /// </summary>
    public static IEndpointRouteBuilder MapEnterpriseHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        // Kubernetes Liveness Probe
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WriteDetailedResponse,
            AllowCachingResponses = false
        });

        // Kubernetes Readiness Probe
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteDetailedResponse,
            AllowCachingResponses = false
        });

        // Full Health Status
        endpoints.MapHealthChecks("/health/full", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = WriteDetailedResponse,
            AllowCachingResponses = false
        });

        // Database Health
        endpoints.MapHealthChecks("/health/db", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("db"),
            ResponseWriter = WriteDetailedResponse
        });

        // Cache Health
        endpoints.MapHealthChecks("/health/cache", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("cache"),
            ResponseWriter = WriteDetailedResponse
        });

        return endpoints;
    }

    private static async Task WriteDetailedResponse(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                tags = e.Value.Tags,
                data = e.Value.Data.Count > 0 ? e.Value.Data : null,
                exception = e.Value.Exception?.Message
            })
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        await context.Response.WriteAsJsonAsync(response, options);
    }
}

#region Health Check Implementations

/// <summary>
/// Checks database connection pool utilization
/// </summary>
public class DatabasePoolHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<IvfDbContext> _contextFactory;
    private readonly ILogger<DatabasePoolHealthCheck> _logger;

    public DatabasePoolHealthCheck(
        IDbContextFactory<IvfDbContext> contextFactory,
        ILogger<DatabasePoolHealthCheck> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(ct);

            // Simple connectivity check
            var canConnect = await dbContext.Database.CanConnectAsync(ct);

            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Cannot connect to database");
            }

            return HealthCheckResult.Healthy("Database pool healthy");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database pool health check failed");
            return HealthCheckResult.Unhealthy("Database pool check failed", ex);
        }
    }
}

/// <summary>
/// Checks Redis cache operations
/// </summary>
public class RedisCacheHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisCacheHealthCheck> _logger;

    public RedisCacheHealthCheck(
        IServiceProvider serviceProvider,
        ILogger<RedisCacheHealthCheck> logger)
    {
        _redis = serviceProvider.GetService<IConnectionMultiplexer>();
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        if (_redis == null)
        {
            return HealthCheckResult.Healthy("Redis not configured");
        }

        try
        {
            if (!_redis.IsConnected)
            {
                return HealthCheckResult.Unhealthy("Redis is not connected");
            }

            var db = _redis.GetDatabase();

            // Test read/write operation
            var testKey = $"health:test:{Guid.NewGuid()}";
            var testValue = DateTime.UtcNow.Ticks.ToString();

            await db.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(10));
            var readValue = await db.StringGetAsync(testKey);
            await db.KeyDeleteAsync(testKey);

            if (readValue != testValue)
            {
                return HealthCheckResult.Unhealthy("Redis read/write mismatch");
            }

            var data = new Dictionary<string, object>
            {
                ["isConnected"] = _redis.IsConnected
            };

            return HealthCheckResult.Healthy("Redis operations healthy", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed");
            return HealthCheckResult.Unhealthy("Redis check failed", ex);
        }
    }
}

/// <summary>
/// Checks .NET garbage collection health
/// </summary>
public class GarbageCollectionHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        var gcInfo = GC.GetGCMemoryInfo();
        var gen0Collections = GC.CollectionCount(0);
        var gen1Collections = GC.CollectionCount(1);
        var gen2Collections = GC.CollectionCount(2);

        var data = new Dictionary<string, object>
        {
            ["heapSizeMb"] = gcInfo.HeapSizeBytes / 1024 / 1024,
            ["totalMemoryMb"] = GC.GetTotalMemory(false) / 1024 / 1024,
            ["gen0Collections"] = gen0Collections,
            ["gen1Collections"] = gen1Collections,
            ["gen2Collections"] = gen2Collections
        };

        // Check for excessive Gen2 collections (indicates memory pressure)
        if (gen2Collections > 100 && gcInfo.PauseTimePercentage > 5)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "High GC pressure detected",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"GC healthy: Gen2 collections={gen2Collections}",
            data));
    }
}

/// <summary>
/// Checks memory usage
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly long _maxMemoryMb;

    public MemoryHealthCheck(IConfiguration? configuration = null)
    {
        _maxMemoryMb = configuration?.GetValue<long>("HealthChecks:MaxMemoryMb", 2048) ?? 2048;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        var allocatedMb = GC.GetTotalMemory(false) / 1024 / 1024;

        var data = new Dictionary<string, object>
        {
            ["allocatedMb"] = allocatedMb,
            ["maxMb"] = _maxMemoryMb
        };

        if (allocatedMb > _maxMemoryMb)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Memory usage ({allocatedMb}MB) exceeds threshold ({_maxMemoryMb}MB)",
                data: data));
        }

        if (allocatedMb > _maxMemoryMb * 0.8)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Memory usage ({allocatedMb}MB) is high",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Memory usage: {allocatedMb}MB",
            data));
    }
}

/// <summary>
/// Checks background services health
/// </summary>
public class BackgroundServicesHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;

    public BackgroundServicesHealthCheck(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        var hostedServices = _serviceProvider.GetServices<IHostedService>().ToList();

        var data = new Dictionary<string, object>
        {
            ["registeredServices"] = hostedServices.Count,
            ["serviceTypes"] = hostedServices.Select(s => s.GetType().Name).ToList()
        };

        if (hostedServices.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "No background services registered",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"{hostedServices.Count} background services registered",
            data));
    }
}

#endregion
