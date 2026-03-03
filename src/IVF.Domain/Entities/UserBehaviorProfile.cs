using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// User behavior profile for adaptive/contextual authentication.
/// Tracks typical login patterns, common devices, and access habits.
/// Inspired by Google Advanced Protection + AWS GuardDuty behavioral baselines.
/// </summary>
public class UserBehaviorProfile : BaseEntity
{
    public Guid UserId { get; private set; }
    public string? TypicalLoginHours { get; private set; } // JSON: {"start":7,"end":19,"timezone":"Asia/Ho_Chi_Minh"}
    public string? CommonIpAddresses { get; private set; } // JSON: ["192.168.1.1","10.0.0.5"]
    public string? CommonCountries { get; private set; } // JSON: ["VN"]
    public string? CommonDeviceFingerprints { get; private set; } // JSON: ["abc123","def456"]
    public string? CommonUserAgents { get; private set; } // JSON: ["Chrome/Windows","Firefox/Mac"]
    public decimal AverageSessionDurationMinutes { get; private set; }
    public int TotalLogins { get; private set; }
    public int TotalFailedLogins { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public DateTime? LastFailedLoginAt { get; private set; }
    public DateTime? LastProfileUpdateAt { get; private set; }
    public string? RiskFactors { get; private set; } // JSON: accumulated risk factors

    private UserBehaviorProfile() { }

    public static UserBehaviorProfile Create(Guid userId)
    {
        return new UserBehaviorProfile
        {
            UserId = userId,
            LastProfileUpdateAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(
        string? typicalLoginHours,
        string? commonIpAddresses,
        string? commonCountries,
        string? commonDeviceFingerprints,
        string? commonUserAgents,
        decimal averageSessionDurationMinutes,
        int totalLogins,
        int totalFailedLogins,
        DateTime? lastLoginAt,
        DateTime? lastFailedLoginAt)
    {
        TypicalLoginHours = typicalLoginHours;
        CommonIpAddresses = commonIpAddresses;
        CommonCountries = commonCountries;
        CommonDeviceFingerprints = commonDeviceFingerprints;
        CommonUserAgents = commonUserAgents;
        AverageSessionDurationMinutes = averageSessionDurationMinutes;
        TotalLogins = totalLogins;
        TotalFailedLogins = totalFailedLogins;
        LastLoginAt = lastLoginAt;
        LastFailedLoginAt = lastFailedLoginAt;
        LastProfileUpdateAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void SetRiskFactors(string? riskFactors)
    {
        RiskFactors = riskFactors;
        SetUpdated();
    }
}
