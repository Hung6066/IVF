using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Persistent user session tracking — enterprise-grade session management
/// inspired by Google's session infrastructure and AWS IAM session policies.
/// Replaces in-memory-only session tracking with durable, queryable records.
/// </summary>
public class UserSession : BaseEntity
{
    public Guid UserId { get; private set; }
    public string SessionToken { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? DeviceFingerprint { get; private set; }
    public string? Country { get; private set; }
    public string? City { get; private set; }
    public string? DeviceType { get; private set; } // Desktop, Mobile, Tablet, API
    public string? OperatingSystem { get; private set; }
    public string? Browser { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime LastActivityAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public string? RevokedReason { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedBy { get; private set; } // "system", "user", "admin", userId

    private UserSession() { }

    public static UserSession Create(
        Guid userId,
        string sessionToken,
        DateTime expiresAt,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceFingerprint = null,
        string? country = null,
        string? city = null,
        string? deviceType = null,
        string? operatingSystem = null,
        string? browser = null)
    {
        return new UserSession
        {
            UserId = userId,
            SessionToken = sessionToken,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceFingerprint = deviceFingerprint,
            Country = country,
            City = city,
            DeviceType = deviceType,
            OperatingSystem = operatingSystem,
            Browser = browser,
            StartedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            LastActivityAt = DateTime.UtcNow,
            IsRevoked = false
        };
    }

    public bool IsActive() => !IsRevoked && !IsDeleted && ExpiresAt > DateTime.UtcNow;

    public void RecordActivity()
    {
        LastActivityAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Revoke(string reason, string revokedBy)
    {
        IsRevoked = true;
        RevokedReason = reason;
        RevokedAt = DateTime.UtcNow;
        RevokedBy = revokedBy;
        SetUpdated();
    }

    public void Extend(DateTime newExpiresAt)
    {
        ExpiresAt = newExpiresAt;
        SetUpdated();
    }
}
