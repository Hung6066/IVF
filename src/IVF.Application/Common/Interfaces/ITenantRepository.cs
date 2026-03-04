using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task<(IReadOnlyList<Tenant> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? search, TenantStatus? status, CancellationToken ct = default);
    Task AddAsync(Tenant tenant, CancellationToken ct = default);

    // Subscriptions
    Task<TenantSubscription?> GetActiveSubscriptionAsync(Guid tenantId, CancellationToken ct = default);
    Task AddSubscriptionAsync(TenantSubscription subscription, CancellationToken ct = default);

    // Usage
    Task<TenantUsageRecord?> GetCurrentUsageAsync(Guid tenantId, CancellationToken ct = default);
    Task AddUsageRecordAsync(TenantUsageRecord usage, CancellationToken ct = default);

    // Stats
    Task<int> GetTenantUserCountAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> GetTenantPatientCountAsync(Guid tenantId, CancellationToken ct = default);
    Task<decimal> GetTotalMonthlyRevenueAsync(CancellationToken ct = default);
    Task<List<TenantUsageRecord>> GetAllCurrentUsagesAsync(CancellationToken ct = default);

    // List with related data (for admin queries)
    Task<SubscriptionPlan?> GetTenantActiveSubscriptionPlanAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<Tenant>> GetAllTenantsRawAsync(CancellationToken ct = default);
}
