# IVF System - Enterprise Upgrade Plan

**Version:** 1.0
**Date:** 2026-03-13
**Target:** Enterprise Scale (10,000+ concurrent users)

---

## Executive Summary

| Area | Current | Target | Priority |
|------|---------|--------|----------|
| Performance | 100 concurrent users | 10,000+ users | P1 |
| Reliability | 95% uptime | 99.99% uptime | P1 |
| Security | 95/100 score | 98/100 score | P2 |
| Maintainability | Monolithic | Modular | P2 |
| Test Coverage | ~20% | 80%+ | P3 |

---

## Phase 1: Critical Infrastructure (Week 1-2)

### 1.1 Connection Pooling & Resources

**File:** `docker-compose.stack.yml`

```yaml
# BEFORE: No pool configuration
ConnectionStrings__DefaultConnection=Host=db;Database=ivf_db;...

# AFTER: Enterprise pooling
ConnectionStrings__DefaultConnection=Host=db;Database=ivf_db;...;Maximum Pool Size=200;Minimum Pool Size=20;Connection Idle Lifetime=300;Connection Pruning Interval=10;Keepalive=30;
```

**Resource Limits Update:**
```yaml
services:
  api:
    deploy:
      replicas: 4  # Was: 2
      resources:
        limits:
          cpus: '2'
          memory: 2G  # Was: 1G
        reservations:
          cpus: '0.5'
          memory: 512M

  db:
    deploy:
      resources:
        limits:
          memory: 8G  # Was: 2G
        reservations:
          memory: 4G

  redis:
    command: >
      redis-server
      --maxmemory 1gb
      --maxmemory-policy allkeys-lru
      --tcp-keepalive 300
      --timeout 0
```

### 1.2 Resilience Policies (Polly)

**New File:** `src/IVF.API/Extensions/ResilienceExtensions.cs`

```csharp
using Polly;
using Polly.Extensions.Http;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace IVF.API.Extensions;

public static class ResilienceExtensions
{
    public static IServiceCollection AddResiliencePolicies(this IServiceCollection services)
    {
        // Circuit Breaker Policy
        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (result, duration) =>
                {
                    Log.Warning("Circuit breaker opened for {Duration}s", duration.TotalSeconds);
                },
                onReset: () => Log.Information("Circuit breaker reset"));

        // Retry Policy with Exponential Backoff
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt)) +
                    TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                onRetry: (result, delay, attempt, context) =>
                {
                    Log.Warning("Retry {Attempt} after {Delay}ms", attempt, delay.TotalMilliseconds);
                });

        // Timeout Policy
        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(30),
            TimeoutStrategy.Optimistic);

        // Bulkhead Isolation
        var bulkheadPolicy = Policy.BulkheadAsync<HttpResponseMessage>(
            maxParallelization: 100,
            maxQueuingActions: 50,
            onBulkheadRejectedAsync: context =>
            {
                Log.Warning("Bulkhead rejected request");
                return Task.CompletedTask;
            });

        // Combined Policy
        var policyWrap = Policy.WrapAsync(
            bulkheadPolicy,
            circuitBreakerPolicy,
            retryPolicy,
            timeoutPolicy);

        services.AddSingleton<IAsyncPolicy<HttpResponseMessage>>(policyWrap);

        return services;
    }

    public static IHttpClientBuilder AddResilienceHandler(
        this IHttpClientBuilder builder,
        IServiceProvider sp)
    {
        var policy = sp.GetRequiredService<IAsyncPolicy<HttpResponseMessage>>();
        return builder.AddPolicyHandler(policy);
    }
}
```

### 1.3 Comprehensive Health Checks

**New File:** `src/IVF.API/Extensions/HealthCheckExtensions.cs`

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace IVF.API.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddEnterpriseHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks()
            // Database
            .AddNpgSql(
                configuration.GetConnectionString("DefaultConnection")!,
                name: "postgresql",
                healthQuery: "SELECT 1",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "db", "critical", "ready" })

            // Redis
            .AddRedis(
                configuration.GetConnectionString("Redis") ?? "localhost:6379",
                name: "redis",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "cache", "ready" })

            // MinIO
            .AddUrlGroup(
                new Uri(configuration["MinIO:Endpoint"] + "/minio/health/live"),
                name: "minio",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "storage", "ready" })

            // SignServer
            .AddUrlGroup(
                new Uri(configuration["Signing:SignServerUrl"] + "/signserver/healthcheck/signserverhealthcheck"),
                name: "signserver",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "pki", "ready" })

            // Memory
            .AddProcessAllocatedMemoryHealthCheck(
                maximumMegabytesAllocated: 1500,
                name: "memory",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "system" })

            // Disk Space
            .AddDiskStorageHealthCheck(
                setup: options => options.AddDrive("/", 1024),
                name: "disk",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "system" })

            // Custom: Database Pool
            .AddCheck<DatabasePoolHealthCheck>(
                "db-pool",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "db", "performance" })

            // Custom: Background Services
            .AddCheck<BackgroundServicesHealthCheck>(
                "background-services",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "services" });

        return services;
    }
}

public class DatabasePoolHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<IvfDbContext> _contextFactory;

    public DatabasePoolHealthCheck(IDbContextFactory<IvfDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(ct);

            // Check pool statistics
            var poolStats = await dbContext.Database
                .SqlQueryRaw<PoolStats>(@"
                    SELECT
                        numbackends as ActiveConnections,
                        (SELECT setting::int FROM pg_settings WHERE name = 'max_connections') as MaxConnections
                    FROM pg_stat_database
                    WHERE datname = current_database()")
                .FirstOrDefaultAsync(ct);

            if (poolStats == null)
                return HealthCheckResult.Unhealthy("Cannot retrieve pool statistics");

            var utilizationPercent = (poolStats.ActiveConnections * 100) / poolStats.MaxConnections;

            if (utilizationPercent > 90)
                return HealthCheckResult.Unhealthy($"Pool utilization critical: {utilizationPercent}%");

            if (utilizationPercent > 70)
                return HealthCheckResult.Degraded($"Pool utilization high: {utilizationPercent}%");

            return HealthCheckResult.Healthy($"Pool utilization: {utilizationPercent}%");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database pool check failed", ex);
        }
    }

    private record PoolStats(int ActiveConnections, int MaxConnections);
}
```

---

## Phase 2: Caching Layer (Week 2-3)

### 2.1 Distributed Cache Service

**New File:** `src/IVF.Infrastructure/Caching/DistributedCacheService.cs`

```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace IVF.Infrastructure.Caching;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default);
    Task InvalidateByPrefixAsync(string prefix, CancellationToken ct = default);
}

public class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private static readonly DistributedCacheEntryOptions DefaultOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
        SlidingExpiration = TimeSpan.FromMinutes(10)
    };

    public DistributedCacheService(
        IDistributedCache cache,
        IConnectionMultiplexer redis,
        ILogger<DistributedCacheService> logger)
    {
        _cache = cache;
        _redis = redis;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var data = await _cache.GetStringAsync(key, ct);
            if (string.IsNullOrEmpty(data))
                return default;

            return JsonSerializer.Deserialize<T>(data, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache get failed for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        try
        {
            var options = expiry.HasValue
                ? new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry }
                : DefaultOptions;

            var data = JsonSerializer.Serialize(value, _jsonOptions);
            await _cache.SetStringAsync(key, data, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache set failed for key {Key}", key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
            return cached;

        var value = await factory();
        await SetAsync(key, value, expiry, ct);
        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(key, ct);
    }

    public async Task InvalidateByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{prefix}*").ToArray();

            if (keys.Length > 0)
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(keys);
                _logger.LogInformation("Invalidated {Count} cache keys with prefix {Prefix}", keys.Length, prefix);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache invalidation failed for prefix {Prefix}", prefix);
        }
    }
}

// Cache Keys Builder
public static class CacheKeys
{
    public static string Tenant(Guid tenantId) => $"tenant:{tenantId}";
    public static string TenantFeatures(Guid tenantId) => $"tenant:{tenantId}:features";
    public static string UserPermissions(Guid userId) => $"user:{userId}:permissions";
    public static string UserSession(Guid userId) => $"user:{userId}:session";
    public static string IpWhitelist(Guid tenantId) => $"security:ip-whitelist:{tenantId}";
    public static string GeoBlockRules(Guid tenantId) => $"security:geo-rules:{tenantId}";
    public static string ServiceCatalog(Guid tenantId) => $"catalog:{tenantId}";
    public static string FormTemplate(Guid formId) => $"form:template:{formId}";
    public static string Patient(Guid patientId) => $"patient:{patientId}";
}
```

### 2.2 Cached Security Middleware

**Update:** `src/IVF.API/Middleware/SecurityEnforcementMiddleware.cs`

```csharp
// Add caching for IP whitelist and Geo rules
public class SecurityEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICacheService _cache;
    private readonly ILogger<SecurityEnforcementMiddleware> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public SecurityEnforcementMiddleware(
        RequestDelegate next,
        ICacheService cache,
        ILogger<SecurityEnforcementMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IvfDbContext db)
    {
        var tenantId = context.User.GetTenantId();
        var clientIp = context.GetClientIpAddress();

        // Cached IP Whitelist Check
        var isWhitelisted = await IsIpWhitelistedCachedAsync(db, tenantId, clientIp);
        if (!isWhitelisted && await IsIpBlockedAsync(db, tenantId, clientIp))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "IP blocked" });
            return;
        }

        // Cached Geo-Blocking Check
        var country = context.Request.Headers["CF-IPCountry"].FirstOrDefault();
        if (!string.IsNullOrEmpty(country))
        {
            var isBlocked = await IsCountryBlockedCachedAsync(db, tenantId, country);
            if (isBlocked)
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { error = "Region blocked" });
                return;
            }
        }

        await _next(context);
    }

    private async Task<bool> IsIpWhitelistedCachedAsync(
        IvfDbContext db, Guid? tenantId, string clientIp)
    {
        var cacheKey = CacheKeys.IpWhitelist(tenantId ?? Guid.Empty);

        var whitelist = await _cache.GetOrSetAsync(
            cacheKey,
            async () => await db.IpWhitelistEntries
                .Where(e => e.IsActive && !e.IsDeleted)
                .Where(e => e.TenantId == null || e.TenantId == tenantId)
                .Select(e => new { e.IpAddress, e.CidrRange })
                .ToListAsync(),
            CacheDuration);

        return whitelist.Any(entry => IsIpInRange(clientIp, entry.IpAddress, entry.CidrRange));
    }

    private async Task<bool> IsCountryBlockedCachedAsync(
        IvfDbContext db, Guid? tenantId, string country)
    {
        var cacheKey = CacheKeys.GeoBlockRules(tenantId ?? Guid.Empty);

        var blockedCountries = await _cache.GetOrSetAsync(
            cacheKey,
            async () => await db.GeoBlockRules
                .Where(r => r.IsActive && !r.IsDeleted)
                .Where(r => r.TenantId == null || r.TenantId == tenantId)
                .Select(r => r.CountryCode)
                .ToListAsync(),
            CacheDuration);

        return blockedCountries.Contains(country, StringComparer.OrdinalIgnoreCase);
    }
}
```

---

## Phase 3: Code Refactoring (Week 3-4)

### 3.1 Program.cs Modularization

**Split into multiple extension files:**

```
src/IVF.API/Extensions/
├── AuthenticationExtensions.cs      # JWT, FIDO2, API Key auth
├── AuthorizationExtensions.cs       # Policies, roles, permissions
├── CachingExtensions.cs             # Redis, memory cache
├── DatabaseExtensions.cs            # EF Core, connection pooling
├── HealthCheckExtensions.cs         # All health checks
├── HttpClientExtensions.cs          # Typed HTTP clients
├── LoggingExtensions.cs             # Serilog, OpenTelemetry
├── MessagingExtensions.cs           # SignalR hubs
├── ResilienceExtensions.cs          # Polly policies
├── SecurityExtensions.cs            # CORS, headers, middleware
├── SwaggerExtensions.cs             # OpenAPI documentation
└── ServiceRegistrationExtensions.cs # Domain services, repositories
```

**New Program.cs (Clean):**

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure Services (modular)
builder.Services
    .AddEnterpriseLogging(builder.Configuration)
    .AddEnterpriseDatabase(builder.Configuration)
    .AddEnterpriseCaching(builder.Configuration)
    .AddEnterpriseAuthentication(builder.Configuration)
    .AddEnterpriseAuthorization()
    .AddEnterpriseHealthChecks(builder.Configuration)
    .AddResiliencePolicies()
    .AddHttpClients(builder.Configuration)
    .AddSignalRHubs()
    .AddDomainServices()
    .AddRepositories()
    .AddBackgroundServices()
    .AddSwaggerDocumentation();

var app = builder.Build();

// Configure Pipeline (modular)
app.UseEnterpriseExceptionHandling()
   .UseEnterpriseSecurity()
   .UseEnterpriseLogging()
   .UseEnterpriseHealthChecks()
   .UseAuthentication()
   .UseAuthorization()
   .UseRateLimiting()
   .MapEnterpriseEndpoints()
   .MapSignalRHubs()
   .MapHealthChecks();

await app.RunAsync();
```

### 3.2 Generic Repository Pattern

**New File:** `src/IVF.Infrastructure/Repositories/GenericRepository.cs`

```csharp
namespace IVF.Infrastructure.Repositories;

public interface IRepository<TEntity> where TEntity : class, IEntity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<PagedResult<TEntity>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}

public class GenericRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, IEntity
{
    protected readonly IvfDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;
    protected readonly ICacheService _cache;
    protected readonly ILogger _logger;

    public GenericRepository(
        IvfDbContext context,
        ICacheService cache,
        ILogger<GenericRepository<TEntity>> logger)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
        _cache = cache;
        _logger = logger;
    }

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(predicate)
            .ToListAsync(ct);
    }

    public virtual async Task<PagedResult<TEntity>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var totalCount = await _dbSet.CountAsync(ct);
        var items = await _dbSet
            .AsNoTracking()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<TEntity>(items, totalCount, page, pageSize);
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _dbSet.FindAsync(new object[] { id }, ct);
        if (entity != null)
        {
            if (entity is ISoftDelete softDelete)
            {
                softDelete.IsDeleted = true;
                softDelete.DeletedAt = DateTime.UtcNow;
                _dbSet.Update(entity);
            }
            else
            {
                _dbSet.Remove(entity);
            }
            await _context.SaveChangesAsync(ct);
        }
    }

    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(e => e.Id == id, ct);
    }

    public virtual async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _dbSet.CountAsync(ct);
    }
}

// Paged Result
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
```

---

## Phase 4: Database Optimization (Week 4-5)

### 4.1 Add Missing Indexes

**New Migration:** `AddEnterpriseIndexes`

```csharp
public partial class AddEnterpriseIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Patients - frequently queried
        migrationBuilder.CreateIndex(
            name: "IX_Patients_TenantId_MRN",
            table: "Patients",
            columns: new[] { "TenantId", "MRN" });

        migrationBuilder.CreateIndex(
            name: "IX_Patients_TenantId_Name",
            table: "Patients",
            columns: new[] { "TenantId", "FullName" })
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        // Cycles - date range queries
        migrationBuilder.CreateIndex(
            name: "IX_Cycles_TenantId_StartDate",
            table: "Cycles",
            columns: new[] { "TenantId", "StartDate" });

        // Audit Logs - time-based queries (partitioned)
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_Timestamp_UserId",
            table: "AuditLogs",
            columns: new[] { "Timestamp", "UserId" });

        // Sessions - active sessions lookup
        migrationBuilder.CreateIndex(
            name: "IX_Sessions_UserId_IsActive",
            table: "Sessions",
            columns: new[] { "UserId", "IsActive" },
            filter: "\"IsActive\" = true");

        // Refresh Tokens - token lookup
        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_TokenHash",
            table: "RefreshTokens",
            column: "TokenHash",
            unique: true);

        // Queue - active queue items
        migrationBuilder.CreateIndex(
            name: "IX_Queues_TenantId_Status_Date",
            table: "Queues",
            columns: new[] { "TenantId", "Status", "CreatedAt" },
            filter: "\"Status\" IN ('Waiting', 'Called')");
    }
}
```

### 4.2 Row-Level Security (RLS)

```sql
-- Enable RLS for multi-tenant tables
ALTER TABLE patients ENABLE ROW LEVEL SECURITY;
ALTER TABLE couples ENABLE ROW LEVEL SECURITY;
ALTER TABLE cycles ENABLE ROW LEVEL SECURITY;

-- Create tenant isolation policy
CREATE POLICY tenant_isolation_patients ON patients
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

CREATE POLICY tenant_isolation_couples ON couples
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

CREATE POLICY tenant_isolation_cycles ON cycles
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

-- Set tenant context function
CREATE OR REPLACE FUNCTION set_tenant_context(tenant_uuid uuid)
RETURNS void AS $$
BEGIN
    PERFORM set_config('app.current_tenant_id', tenant_uuid::text, true);
END;
$$ LANGUAGE plpgsql;
```

---

## Phase 5: Infrastructure Scaling (Week 5-6)

### 5.1 Redis Cluster Mode

**Update:** `docker-compose.stack.yml`

```yaml
services:
  redis-master:
    image: redis:7-alpine
    command: >
      redis-server
      --requirepass ${REDIS_PASSWORD}
      --maxmemory 2gb
      --maxmemory-policy allkeys-lru
      --appendonly yes
      --cluster-enabled yes
      --cluster-config-file nodes.conf
      --cluster-node-timeout 5000
    deploy:
      replicas: 3
      placement:
        constraints:
          - node.role == manager

  redis-sentinel:
    image: redis:7-alpine
    command: redis-sentinel /etc/redis/sentinel.conf
    configs:
      - source: redis-sentinel-config
        target: /etc/redis/sentinel.conf
    deploy:
      replicas: 3
```

### 5.2 PostgreSQL Read Replicas

```yaml
services:
  db-read-1:
    image: postgres:16-alpine
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/ivf_db_password
    command: |
      postgres
      -c hot_standby=on
      -c max_standby_streaming_delay=30s
      -c wal_receiver_status_interval=10s
    deploy:
      placement:
        constraints:
          - node.labels.db-role == read-replica

  db-read-2:
    image: postgres:16-alpine
    # Same config as db-read-1
    deploy:
      placement:
        constraints:
          - node.labels.db-role == read-replica

  # PgBouncer for connection pooling
  pgbouncer:
    image: edoburu/pgbouncer:latest
    environment:
      DATABASE_URL: postgres://postgres:${DB_PASSWORD}@db:5432/ivf_db
      POOL_MODE: transaction
      MAX_CLIENT_CONN: 1000
      DEFAULT_POOL_SIZE: 50
      RESERVE_POOL_SIZE: 25
    deploy:
      replicas: 2
```

### 5.3 Auto-Scaling Configuration (Kubernetes)

**File:** `k8s/api-hpa.yaml`

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: ivf-api-hpa
  namespace: ivf
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: ivf-api
  minReplicas: 4
  maxReplicas: 20
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
    - type: Pods
      pods:
        metric:
          name: http_requests_per_second
        target:
          type: AverageValue
          averageValue: 1000
  behavior:
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
        - type: Pods
          value: 4
          periodSeconds: 60
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
        - type: Percent
          value: 25
          periodSeconds: 120
```

---

## Phase 6: Monitoring & Observability (Week 6-7)

### 6.1 Enhanced Prometheus Rules

```yaml
groups:
  - name: enterprise-alerts
    rules:
      # API Performance
      - alert: HighLatencyP99
        expr: histogram_quantile(0.99, rate(http_request_duration_seconds_bucket[5m])) > 2
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "P99 latency above 2s"

      - alert: ErrorRateHigh
        expr: sum(rate(http_requests_total{status=~"5.."}[5m])) / sum(rate(http_requests_total[5m])) > 0.01
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "Error rate above 1%"

      # Database
      - alert: DatabaseConnectionPoolExhausted
        expr: pg_stat_activity_count / pg_settings_max_connections > 0.9
        for: 2m
        labels:
          severity: critical

      - alert: SlowQueries
        expr: rate(pg_stat_statements_seconds_total[5m]) > 1
        for: 5m
        labels:
          severity: warning

      # Redis
      - alert: RedisMemoryHigh
        expr: redis_memory_used_bytes / redis_memory_max_bytes > 0.9
        for: 5m
        labels:
          severity: warning

      # Circuit Breaker
      - alert: CircuitBreakerOpen
        expr: circuit_breaker_state{state="open"} == 1
        for: 1m
        labels:
          severity: critical
```

### 6.2 SLO Dashboard

```yaml
# Grafana Dashboard JSON
{
  "title": "IVF Enterprise SLOs",
  "panels": [
    {
      "title": "Availability SLO (99.99%)",
      "targets": [
        {
          "expr": "1 - (sum(rate(http_requests_total{status=~\"5..\"}[30d])) / sum(rate(http_requests_total[30d])))"
        }
      ]
    },
    {
      "title": "Latency SLO (P99 < 500ms)",
      "targets": [
        {
          "expr": "histogram_quantile(0.99, sum(rate(http_request_duration_seconds_bucket[30d])) by (le))"
        }
      ]
    },
    {
      "title": "Error Budget Remaining",
      "targets": [
        {
          "expr": "1 - (sum(increase(http_requests_total{status=~\"5..\"}[30d])) / (sum(increase(http_requests_total[30d])) * 0.0001))"
        }
      ]
    }
  ]
}
```

---

## Summary: Implementation Timeline

| Phase | Duration | Focus | Deliverables |
|-------|----------|-------|--------------|
| **Phase 1** | Week 1-2 | Critical Infrastructure | Connection pooling, Polly, Health checks |
| **Phase 2** | Week 2-3 | Caching | Redis cache layer, Cached middleware |
| **Phase 3** | Week 3-4 | Refactoring | Modular Program.cs, Generic repos |
| **Phase 4** | Week 4-5 | Database | Indexes, RLS, Query optimization |
| **Phase 5** | Week 5-6 | Scaling | Redis cluster, Read replicas, HPA |
| **Phase 6** | Week 6-7 | Monitoring | SLOs, Enhanced alerts, Dashboards |

## Expected Results

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Concurrent Users | 100 | 10,000+ | **100x** |
| P99 Latency | 2s | <200ms | **10x** |
| Error Rate | 1% | <0.01% | **100x** |
| Uptime | 95% | 99.99% | **5x** |
| Code Maintainability | Low | High | Modular |
| Test Coverage | 20% | 80% | **4x** |
| Security Score | 95/100 | 98/100 | +3 |
