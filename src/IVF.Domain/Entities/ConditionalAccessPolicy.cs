using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Conditional Access Policy — inspired by Microsoft Entra Conditional Access + Google BeyondCorp.
/// Defines access rules based on device, location, time, role, and risk context.
/// </summary>
public class ConditionalAccessPolicy : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsEnabled { get; private set; } = true;
    public int Priority { get; private set; } // Lower = higher priority

    // Conditions (JSON arrays for flexibility)
    public string? TargetRoles { get; private set; } // JSON: ["Admin","Doctor"]
    public string? TargetUsers { get; private set; } // JSON: ["userId1","userId2"] or null = all
    public string? RequiredDeviceTrust { get; private set; } // "Trusted", "PartiallyTrusted", etc.
    public string? AllowedCountries { get; private set; } // JSON: ["VN","US"] or null = any
    public string? BlockedCountries { get; private set; } // JSON: ["KP","IR"]
    public string? AllowedIpRanges { get; private set; } // JSON: ["10.0.0.0/8","192.168.1.0/24"]
    public string? AllowedTimeWindows { get; private set; } // JSON: [{"start":"06:00","end":"22:00","days":["Mon","Tue",...]}]
    public string? MaxRiskLevel { get; private set; } // "Low","Medium","High" — block if above
    public bool RequireMfa { get; private set; }
    public bool RequireCompliantDevice { get; private set; }
    public bool BlockVpnTor { get; private set; }

    // Actions
    public string Action { get; private set; } = "Allow"; // Allow, Block, RequireMfa, RequireStepUp, RequireDeviceCompliance
    public string? CustomMessage { get; private set; }

    // Audit
    public Guid? CreatedBy { get; private set; }
    public Guid? LastModifiedBy { get; private set; }

    private ConditionalAccessPolicy() { }

    public static ConditionalAccessPolicy Create(
        string name,
        string? description,
        int priority,
        string action,
        Guid? createdBy)
    {
        return new ConditionalAccessPolicy
        {
            Name = name,
            Description = description,
            Priority = priority,
            Action = action,
            CreatedBy = createdBy
        };
    }

    public void Update(
        string name,
        string? description,
        int priority,
        string action,
        bool isEnabled,
        Guid? modifiedBy)
    {
        Name = name;
        Description = description;
        Priority = priority;
        Action = action;
        IsEnabled = isEnabled;
        LastModifiedBy = modifiedBy;
        SetUpdated();
    }

    public void SetConditions(
        string? targetRoles = null,
        string? targetUsers = null,
        string? requiredDeviceTrust = null,
        string? allowedCountries = null,
        string? blockedCountries = null,
        string? allowedIpRanges = null,
        string? allowedTimeWindows = null,
        string? maxRiskLevel = null,
        bool requireMfa = false,
        bool requireCompliantDevice = false,
        bool blockVpnTor = false,
        string? customMessage = null)
    {
        TargetRoles = targetRoles;
        TargetUsers = targetUsers;
        RequiredDeviceTrust = requiredDeviceTrust;
        AllowedCountries = allowedCountries;
        BlockedCountries = blockedCountries;
        AllowedIpRanges = allowedIpRanges;
        AllowedTimeWindows = allowedTimeWindows;
        MaxRiskLevel = maxRiskLevel;
        RequireMfa = requireMfa;
        RequireCompliantDevice = requireCompliantDevice;
        BlockVpnTor = blockVpnTor;
        CustomMessage = customMessage;
        SetUpdated();
    }

    public void Enable() { IsEnabled = true; SetUpdated(); }
    public void Disable() { IsEnabled = false; SetUpdated(); }
}
