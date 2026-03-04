using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Join entity: maps a PlanDefinition to a FeatureDefinition.
/// When a tenant subscribes to a plan, they get all features linked to that plan.
/// </summary>
public class PlanFeature : BaseEntity
{
    public Guid PlanDefinitionId { get; private set; }
    public Guid FeatureDefinitionId { get; private set; }

    /// <summary>Display order of this feature within the plan's feature list.</summary>
    public int SortOrder { get; private set; }

    // Navigation
    public PlanDefinition PlanDefinition { get; private set; } = null!;
    public FeatureDefinition FeatureDefinition { get; private set; } = null!;

    private PlanFeature() { }

    public static PlanFeature Create(Guid planDefinitionId, Guid featureDefinitionId, int sortOrder = 0)
    {
        return new PlanFeature
        {
            PlanDefinitionId = planDefinitionId,
            FeatureDefinitionId = featureDefinitionId,
            SortOrder = sortOrder
        };
    }
}
