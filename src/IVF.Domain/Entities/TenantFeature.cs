using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Per-tenant feature enablement. Auto-populated from plan features on subscription,
/// but can be overridden per-tenant (e.g. Custom plans, trials, promotions).
/// </summary>
public class TenantFeature : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid FeatureDefinitionId { get; private set; }

    /// <summary>Whether this feature is enabled for the tenant.</summary>
    public bool IsEnabled { get; private set; } = true;

    // Navigation
    public Tenant Tenant { get; private set; } = null!;
    public FeatureDefinition FeatureDefinition { get; private set; } = null!;

    private TenantFeature() { }

    public static TenantFeature Create(Guid tenantId, Guid featureDefinitionId, bool isEnabled = true)
    {
        return new TenantFeature
        {
            TenantId = tenantId,
            FeatureDefinitionId = featureDefinitionId,
            IsEnabled = isEnabled
        };
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        SetUpdated();
    }
}
