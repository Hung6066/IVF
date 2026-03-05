using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Pricing.Queries;

public class GetDynamicPricingQueryHandler : IRequestHandler<GetDynamicPricingQuery, List<PlanPricingDto>>
{
    private readonly IPricingRepository _repo;

    public GetDynamicPricingQueryHandler(IPricingRepository repo) => _repo = repo;

    public async Task<List<PlanPricingDto>> Handle(GetDynamicPricingQuery request, CancellationToken ct)
    {
        var plans = await _repo.GetActivePlansWithFeaturesAsync(ct);

        return plans
            .Where(p => p.Plan != SubscriptionPlan.Custom) // Custom plans not shown on public pricing page
            .OrderBy(p => p.SortOrder)
            .Select(p => new PlanPricingDto(
                Plan: p.Plan.ToString(),
                DisplayName: p.DisplayName,
                Description: p.Description,
                Price: p.MonthlyPrice,
                Currency: p.Currency,
                Duration: p.Duration,
                MaxUsers: p.MaxUsers,
                MaxPatients: p.MaxPatientsPerMonth,
                StorageGb: Math.Round(p.StorageLimitMb / 1024.0, 1),
                IsFeatured: p.IsFeatured,
                Features: p.PlanFeatures
                    .Where(pf => !pf.IsDeleted && pf.FeatureDefinition.IsActive)
                    .OrderBy(pf => pf.SortOrder)
                    .Select(pf => new PlanFeatureDto(
                        pf.FeatureDefinitionId,
                        pf.FeatureDefinition.Code,
                        pf.FeatureDefinition.DisplayName,
                        pf.FeatureDefinition.Description,
                        pf.FeatureDefinition.Icon,
                        pf.FeatureDefinition.Category))
                    .ToList()))
            .ToList();
    }
}

public class GetTenantDynamicFeaturesQueryHandler : IRequestHandler<GetTenantDynamicFeaturesQuery, TenantFeaturesDto>
{
    private readonly IPricingRepository _pricingRepo;
    private readonly ITenantRepository _tenantRepo;

    public GetTenantDynamicFeaturesQueryHandler(IPricingRepository pricingRepo, ITenantRepository tenantRepo)
    {
        _pricingRepo = pricingRepo;
        _tenantRepo = tenantRepo;
    }

    public async Task<TenantFeaturesDto> Handle(GetTenantDynamicFeaturesQuery request, CancellationToken ct)
    {
        if (request.IsPlatformAdmin)
        {
            var allFeatures = await _pricingRepo.GetAllFeaturesAsync(activeOnly: true, ct);
            return new TenantFeaturesDto(
                IsPlatformAdmin: true,
                EnabledFeatures: allFeatures.Select(f => f.Code).ToList(),
                IsolationStrategy: DataIsolationStrategy.SharedDatabase,
                MaxUsers: 999,
                MaxPatients: 99999);
        }

        if (request.TenantId is not { } tenantId || tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("Tenant ID is required");

        var tenant = await _tenantRepo.GetByIdAsync(tenantId, ct)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found");

        var featureCodes = await _pricingRepo.GetTenantFeatureCodesAsync(tenantId, ct);

        return new TenantFeaturesDto(
            IsPlatformAdmin: false,
            EnabledFeatures: featureCodes,
            IsolationStrategy: tenant.IsolationStrategy,
            MaxUsers: tenant.MaxUsers,
            MaxPatients: tenant.MaxPatientsPerMonth);
    }
}

public class GetAllFeatureDefinitionsQueryHandler : IRequestHandler<GetAllFeatureDefinitionsQuery, List<FeatureDefinitionDto>>
{
    private readonly IPricingRepository _repo;

    public GetAllFeatureDefinitionsQueryHandler(IPricingRepository repo) => _repo = repo;

    public async Task<List<FeatureDefinitionDto>> Handle(GetAllFeatureDefinitionsQuery request, CancellationToken ct)
    {
        var features = await _repo.GetAllFeaturesAsync(activeOnly: false, ct);
        return features
            .OrderBy(f => f.Category).ThenBy(f => f.SortOrder)
            .Select(f => new FeatureDefinitionDto(
                f.Id, f.Code, f.DisplayName, f.Description,
                f.Icon, f.Category, f.SortOrder, f.IsActive))
            .ToList();
    }
}

public class GetAllPlanDefinitionsQueryHandler : IRequestHandler<GetAllPlanDefinitionsQuery, List<PlanDefinitionDto>>
{
    private readonly IPricingRepository _repo;

    public GetAllPlanDefinitionsQueryHandler(IPricingRepository repo) => _repo = repo;

    public async Task<List<PlanDefinitionDto>> Handle(GetAllPlanDefinitionsQuery request, CancellationToken ct)
    {
        var plans = await _repo.GetAllPlansWithFeaturesAsync(ct);
        return plans.OrderBy(p => p.SortOrder)
            .Select(p => new PlanDefinitionDto(
                p.Id, p.Plan.ToString(), p.DisplayName, p.Description,
                p.MonthlyPrice, p.Currency, p.Duration,
                p.MaxUsers, p.MaxPatientsPerMonth, p.StorageLimitMb,
                p.SortOrder, p.IsFeatured, p.IsActive,
                p.PlanFeatures
                    .Where(pf => !pf.IsDeleted)
                    .OrderBy(pf => pf.SortOrder)
                    .Select(pf => new PlanFeatureDto(
                        pf.FeatureDefinitionId,
                        pf.FeatureDefinition.Code,
                        pf.FeatureDefinition.DisplayName,
                        pf.FeatureDefinition.Description,
                        pf.FeatureDefinition.Icon,
                        pf.FeatureDefinition.Category))
                    .ToList()))
            .ToList();
    }
}

public class GetTenantFeatureOverridesQueryHandler : IRequestHandler<GetTenantFeatureOverridesQuery, List<TenantFeatureDto>>
{
    private readonly IPricingRepository _pricingRepo;
    private readonly ITenantRepository _tenantRepo;
    private readonly IUnitOfWork _unitOfWork;

    public GetTenantFeatureOverridesQueryHandler(IPricingRepository pricingRepo, ITenantRepository tenantRepo, IUnitOfWork unitOfWork)
    {
        _pricingRepo = pricingRepo;
        _tenantRepo = tenantRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<TenantFeatureDto>> Handle(GetTenantFeatureOverridesQuery request, CancellationToken ct)
    {
        var tenantFeatures = await _pricingRepo.GetTenantFeaturesAsync(request.TenantId, ct);

        // Auto-sync from plan if no features exist yet (legacy tenants created before feature system)
        if (tenantFeatures.Count == 0)
        {
            var plan = await _tenantRepo.GetTenantActiveSubscriptionPlanAsync(request.TenantId, ct);
            if (plan.HasValue)
            {
                await _pricingRepo.SyncTenantFeaturesFromPlanAsync(request.TenantId, plan.Value, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                tenantFeatures = await _pricingRepo.GetTenantFeaturesAsync(request.TenantId, ct);
            }
        }

        return tenantFeatures
            .Select(tf => new TenantFeatureDto(
                tf.FeatureDefinitionId,
                tf.FeatureDefinition.Code,
                tf.FeatureDefinition.DisplayName,
                tf.FeatureDefinition.Icon,
                tf.FeatureDefinition.Category,
                tf.IsEnabled))
            .ToList();
    }
}
