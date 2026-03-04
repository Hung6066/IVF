using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IPricingRepository
{
    Task<List<PlanDefinition>> GetActivePlansWithFeaturesAsync(CancellationToken ct = default);
    Task<PlanDefinition?> GetPlanByTypeAsync(SubscriptionPlan plan, CancellationToken ct = default);
    Task<List<FeatureDefinition>> GetAllFeaturesAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<List<string>> GetTenantFeatureCodesAsync(Guid tenantId, CancellationToken ct = default);
    Task SyncTenantFeaturesFromPlanAsync(Guid tenantId, SubscriptionPlan plan, CancellationToken ct = default);
}
