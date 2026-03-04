using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Tenants.Queries;

public class GetAllTenantsQueryHandler : IRequestHandler<GetAllTenantsQuery, PagedResult<TenantListItemDto>>
{
    private readonly ITenantRepository _repo;

    public GetAllTenantsQueryHandler(ITenantRepository repo) => _repo = repo;

    public async Task<PagedResult<TenantListItemDto>> Handle(GetAllTenantsQuery request, CancellationToken ct)
    {
        var (tenants, totalCount) = await _repo.GetAllAsync(request.Page, request.PageSize, request.Search, request.Status, ct);

        var items = new List<TenantListItemDto>();
        foreach (var t in tenants)
        {
            var plan = await _repo.GetTenantActiveSubscriptionPlanAsync(t.Id, ct);
            var userCount = await _repo.GetTenantUserCountAsync(t.Id, ct);
            var patientCount = await _repo.GetTenantPatientCountAsync(t.Id, ct);
            items.Add(new TenantListItemDto(t.Id, t.Name, t.Slug, t.Status, t.IsolationStrategy, plan, userCount, patientCount, t.CreatedAt));
        }

        return new PagedResult<TenantListItemDto>(items, totalCount, request.Page, request.PageSize);
    }
}

public class GetTenantByIdQueryHandler : IRequestHandler<GetTenantByIdQuery, TenantDto?>
{
    private readonly ITenantRepository _repo;

    public GetTenantByIdQueryHandler(ITenantRepository repo) => _repo = repo;

    public async Task<TenantDto?> Handle(GetTenantByIdQuery request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.Id, ct);
        if (tenant is null) return null;

        var sub = await _repo.GetActiveSubscriptionAsync(tenant.Id, ct);
        var usage = await _repo.GetCurrentUsageAsync(tenant.Id, ct);

        return new TenantDto(
            tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl,
            tenant.Address, tenant.Phone, tenant.Email, tenant.Website, tenant.TaxId,
            tenant.Status, tenant.MaxUsers, tenant.MaxPatientsPerMonth, tenant.StorageLimitMb,
            tenant.AiEnabled, tenant.DigitalSigningEnabled, tenant.BiometricsEnabled, tenant.AdvancedReportingEnabled,
            tenant.IsolationStrategy, tenant.IsRootTenant,
            tenant.DatabaseSchema, tenant.ConnectionString,
            tenant.PrimaryColor, tenant.Locale, tenant.TimeZone, tenant.CustomDomain,
            tenant.CreatedAt,
            sub is null ? null : new SubscriptionDto(
                sub.Id, sub.Plan, sub.Status, sub.BillingCycle,
                sub.MonthlyPrice, sub.DiscountPercent, sub.Currency,
                sub.StartDate, sub.EndDate, sub.TrialEndDate, sub.NextBillingDate,
                sub.AutoRenew, sub.GetEffectivePrice()),
            usage is null ? null : new UsageDto(
                usage.Year, usage.Month, usage.ActiveUsers, usage.NewPatients,
                usage.TreatmentCycles, usage.FormResponses, usage.SignedDocuments,
                usage.StorageUsedMb, usage.ApiCalls));
    }
}

public class GetTenantStatsQueryHandler : IRequestHandler<GetTenantStatsQuery, TenantPlatformStats>
{
    private readonly ITenantRepository _repo;

    public GetTenantStatsQueryHandler(ITenantRepository repo) => _repo = repo;

    public async Task<TenantPlatformStats> Handle(GetTenantStatsQuery request, CancellationToken ct)
    {
        var tenants = await _repo.GetAllTenantsRawAsync(ct);
        var revenue = await _repo.GetTotalMonthlyRevenueAsync(ct);
        var usages = await _repo.GetAllCurrentUsagesAsync(ct);

        return new TenantPlatformStats(
            TotalTenants: tenants.Count,
            ActiveTenants: tenants.Count(t => t.Status == TenantStatus.Active),
            TrialTenants: tenants.Count(t => t.Status == TenantStatus.Trial),
            SuspendedTenants: tenants.Count(t => t.Status == TenantStatus.Suspended),
            MonthlyRevenue: revenue,
            TotalUsers: usages.Sum(u => u.ActiveUsers),
            TotalPatients: usages.Sum(u => u.NewPatients),
            TotalStorageMb: usages.Sum(u => u.StorageUsedMb));
    }
}
