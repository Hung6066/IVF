using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class RateLimitConfig : BaseEntity
{
    public string PolicyName { get; private set; } = string.Empty;
    public string WindowType { get; private set; } = "fixed"; // fixed, sliding, token_bucket
    public int WindowSeconds { get; private set; }
    public int PermitLimit { get; private set; }
    public string? AppliesTo { get; private set; } // endpoint pattern or null = global
    public bool IsEnabled { get; private set; } = true;
    public string? Description { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;

    private RateLimitConfig() { }

    public static RateLimitConfig Create(
        string policyName,
        string windowType,
        int windowSeconds,
        int permitLimit,
        string? appliesTo,
        string createdBy,
        string? description = null)
    {
        return new RateLimitConfig
        {
            PolicyName = policyName,
            WindowType = windowType,
            WindowSeconds = windowSeconds,
            PermitLimit = permitLimit,
            AppliesTo = appliesTo,
            CreatedBy = createdBy,
            Description = description,
            IsEnabled = true
        };
    }

    public void Update(string windowType, int windowSeconds, int permitLimit, string? description)
    {
        WindowType = windowType;
        WindowSeconds = windowSeconds;
        PermitLimit = permitLimit;
        Description = description;
        SetUpdated();
    }

    public void Enable() { IsEnabled = true; SetUpdated(); }
    public void Disable() { IsEnabled = false; SetUpdated(); }
}
