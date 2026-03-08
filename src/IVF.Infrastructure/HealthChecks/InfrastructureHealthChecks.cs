using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minio;
using StackExchange.Redis;

namespace IVF.Infrastructure.HealthChecks;

/// <summary>
/// Health check for Redis connectivity.
/// Executes a PING command to verify the connection is alive.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var latency = await db.PingAsync();

            return latency.TotalMilliseconds < 500
                ? HealthCheckResult.Healthy($"Redis PING: {latency.TotalMilliseconds:F1}ms")
                : HealthCheckResult.Degraded($"Redis PING slow: {latency.TotalMilliseconds:F1}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis unreachable", ex);
        }
    }
}

/// <summary>
/// Health check for MinIO object storage.
/// Verifies connectivity by listing buckets (lightweight operation).
/// </summary>
public sealed class MinioHealthCheck : IHealthCheck
{
    private readonly IMinioClient _minio;

    public MinioHealthCheck(IMinioClient minio) => _minio = minio;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var buckets = await _minio.ListBucketsAsync(ct);
            return HealthCheckResult.Healthy($"MinIO OK: {buckets.Buckets.Count} buckets");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MinIO unreachable", ex);
        }
    }
}

/// <summary>
/// Health check for SignServer digital signing service.
/// Attempts to reach the SignServer health/status endpoint.
/// </summary>
public sealed class SignServerHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public SignServerHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var baseUrl = _configuration["DigitalSigning:SignServerUrl"];
        if (string.IsNullOrEmpty(baseUrl))
            return HealthCheckResult.Healthy("SignServer not configured — skipping");

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/signserver/healthcheck/signserverhealth", ct);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("SignServer reachable")
                : HealthCheckResult.Degraded($"SignServer returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SignServer unreachable", ex);
        }
    }
}
