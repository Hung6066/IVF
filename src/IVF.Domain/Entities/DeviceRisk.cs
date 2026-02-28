using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class DeviceRisk : BaseEntity
{
    public string UserId { get; private set; } = string.Empty;
    public string DeviceId { get; private set; } = string.Empty;
    public RiskLevel RiskLevel { get; private set; }
    public decimal RiskScore { get; private set; }
    public string? Factors { get; private set; }
    public bool IsTrusted { get; private set; }
    public string? IpAddress { get; private set; }
    public string? Country { get; private set; }
    public string? UserAgent { get; private set; }

    private DeviceRisk() { }

    public static DeviceRisk Create(
        string userId,
        string deviceId,
        RiskLevel riskLevel,
        decimal riskScore,
        string? factors,
        bool isTrusted,
        string? ipAddress = null,
        string? country = null,
        string? userAgent = null)
    {
        return new DeviceRisk
        {
            UserId = userId,
            DeviceId = deviceId,
            RiskLevel = riskLevel,
            RiskScore = riskScore,
            Factors = factors,
            IsTrusted = isTrusted,
            IpAddress = ipAddress,
            Country = country,
            UserAgent = userAgent
        };
    }

    public void MarkTrusted()
    {
        IsTrusted = true;
        SetUpdated();
    }

    public void UpdateRisk(RiskLevel riskLevel, decimal riskScore, string? factors)
    {
        RiskLevel = riskLevel;
        RiskScore = riskScore;
        Factors = factors;
        SetUpdated();
    }
}
