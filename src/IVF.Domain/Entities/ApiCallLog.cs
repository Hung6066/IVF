using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Logs individual API calls per tenant for usage monitoring and analytics.
/// </summary>
public class ApiCallLog : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Username { get; private set; }
    public string Method { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public long DurationMs { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime RequestedAt { get; private set; }

    private ApiCallLog() { }

    public static ApiCallLog Create(
        Guid tenantId,
        Guid? userId,
        string? username,
        string method,
        string path,
        int statusCode,
        long durationMs,
        string? ipAddress,
        string? userAgent)
    {
        return new ApiCallLog
        {
            TenantId = tenantId,
            UserId = userId,
            Username = username,
            Method = method,
            Path = path,
            StatusCode = statusCode,
            DurationMs = durationMs,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            RequestedAt = DateTime.UtcNow
        };
    }
}
