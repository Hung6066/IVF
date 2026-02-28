using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class ZTPolicy : BaseEntity
{
    public string Action { get; private set; } = string.Empty;
    public string? RequiredAuthLevel { get; private set; }
    public string? MaxAllowedRisk { get; private set; }
    public bool RequireTrustedDevice { get; private set; }
    public bool RequireFreshSession { get; private set; }
    public bool BlockAnomaly { get; private set; } = true;
    public bool RequireGeoFence { get; private set; }
    public string? AllowedCountries { get; private set; }
    public bool BlockVpnTor { get; private set; }
    public bool AllowBreakGlassOverride { get; private set; } = true;
    public bool IsActive { get; private set; } = true;
    public Guid? UpdatedBy { get; private set; }

    private ZTPolicy() { }

    public static ZTPolicy Create(
        string action,
        string? requiredAuthLevel,
        string? maxAllowedRisk,
        bool requireTrustedDevice = false,
        bool requireFreshSession = false,
        bool blockAnomaly = true,
        bool requireGeoFence = false,
        string? allowedCountries = null,
        bool blockVpnTor = false,
        bool allowBreakGlassOverride = true)
    {
        return new ZTPolicy
        {
            Action = action,
            RequiredAuthLevel = requiredAuthLevel,
            MaxAllowedRisk = maxAllowedRisk,
            RequireTrustedDevice = requireTrustedDevice,
            RequireFreshSession = requireFreshSession,
            BlockAnomaly = blockAnomaly,
            RequireGeoFence = requireGeoFence,
            AllowedCountries = allowedCountries,
            BlockVpnTor = blockVpnTor,
            AllowBreakGlassOverride = allowBreakGlassOverride
        };
    }

    public void Update(
        string? requiredAuthLevel,
        string? maxAllowedRisk,
        bool requireTrustedDevice,
        bool requireFreshSession,
        bool blockAnomaly,
        bool requireGeoFence,
        string? allowedCountries,
        bool blockVpnTor,
        bool allowBreakGlassOverride,
        Guid? updatedBy)
    {
        RequiredAuthLevel = requiredAuthLevel;
        MaxAllowedRisk = maxAllowedRisk;
        RequireTrustedDevice = requireTrustedDevice;
        RequireFreshSession = requireFreshSession;
        BlockAnomaly = blockAnomaly;
        RequireGeoFence = requireGeoFence;
        AllowedCountries = allowedCountries;
        BlockVpnTor = blockVpnTor;
        AllowBreakGlassOverride = allowBreakGlassOverride;
        UpdatedBy = updatedBy;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }
}
