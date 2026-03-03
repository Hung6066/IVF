using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Tracks user login activity and analytics — enterprise user analytics
/// inspired by Google Security Center, Facebook Login Activity, and AWS CloudTrail.
/// Provides per-user activity timeline for compliance, forensics, and UX.
/// </summary>
public class UserLoginHistory : BaseEntity
{
    public Guid UserId { get; private set; }
    public string LoginMethod { get; private set; } = "password"; // password, passkey, mfa, sso, api-key
    public bool IsSuccess { get; private set; }
    public string? FailureReason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? DeviceFingerprint { get; private set; }
    public string? Country { get; private set; }
    public string? City { get; private set; }
    public string? DeviceType { get; private set; }
    public string? OperatingSystem { get; private set; }
    public string? Browser { get; private set; }
    public decimal? RiskScore { get; private set; }
    public bool IsSuspicious { get; private set; }
    public string? RiskFactors { get; private set; } // JSON array
    public TimeSpan? SessionDuration { get; private set; }
    public DateTime LoginAt { get; private set; }
    public DateTime? LogoutAt { get; private set; }

    private UserLoginHistory() { }

    public static UserLoginHistory Create(
        Guid userId,
        string loginMethod,
        bool isSuccess,
        string? failureReason = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceFingerprint = null,
        string? country = null,
        string? city = null,
        string? deviceType = null,
        string? operatingSystem = null,
        string? browser = null,
        decimal? riskScore = null,
        bool isSuspicious = false,
        string? riskFactors = null)
    {
        return new UserLoginHistory
        {
            UserId = userId,
            LoginMethod = loginMethod,
            IsSuccess = isSuccess,
            FailureReason = failureReason,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceFingerprint = deviceFingerprint,
            Country = country,
            City = city,
            DeviceType = deviceType,
            OperatingSystem = operatingSystem,
            Browser = browser,
            RiskScore = riskScore,
            IsSuspicious = isSuspicious,
            RiskFactors = riskFactors,
            LoginAt = DateTime.UtcNow
        };
    }

    public void RecordLogout()
    {
        LogoutAt = DateTime.UtcNow;
        SessionDuration = LogoutAt.Value - LoginAt;
        SetUpdated();
    }
}
