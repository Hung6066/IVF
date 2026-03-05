using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IPricingRepository
{
    Task<List<PlanDefinition>> GetActivePlansWithFeaturesAsync(CancellationToken ct = default);
    Task<List<PlanDefinition>> GetAllPlansWithFeaturesAsync(CancellationToken ct = default);
    Task<PlanDefinition?> GetPlanByIdAsync(Guid id, CancellationToken ct = default);
    Task<PlanDefinition?> GetPlanByTypeAsync(SubscriptionPlan plan, CancellationToken ct = default);
    Task<List<FeatureDefinition>> GetAllFeaturesAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<FeatureDefinition?> GetFeatureByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<string>> GetTenantFeatureCodesAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<TenantFeature>> GetTenantFeaturesAsync(Guid tenantId, CancellationToken ct = default);
    Task SyncTenantFeaturesFromPlanAsync(Guid tenantId, SubscriptionPlan plan, CancellationToken ct = default);

    // CRUD
    Task AddFeatureAsync(FeatureDefinition feature, CancellationToken ct = default);
    Task AddPlanAsync(PlanDefinition plan, CancellationToken ct = default);
    Task AddPlanFeatureAsync(PlanFeature planFeature, CancellationToken ct = default);
    Task AddTenantFeatureAsync(TenantFeature tenantFeature, CancellationToken ct = default);
    Task RemovePlanFeaturesAsync(Guid planDefinitionId, CancellationToken ct = default);
    Task RemoveTenantFeaturesAsync(Guid tenantId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
