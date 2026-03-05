using IVF.Application.Common.Exceptions;
using IVF.Application.Common.Interfaces;

namespace IVF.Infrastructure.Services;

public class TenantLimitService : ITenantLimitService
{
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantRepository _tenantRepo;
    private readonly IPricingRepository _pricingRepo;

    public TenantLimitService(
        ICurrentUserService currentUser,
        ITenantRepository tenantRepo,
        IPricingRepository pricingRepo)
    {
        _currentUser = currentUser;
        _tenantRepo = tenantRepo;
        _pricingRepo = pricingRepo;
    }

    public async Task EnsureUserLimitAsync(CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null || _currentUser.IsPlatformAdmin) return;

        var (maxUsers, _, _) = await GetEffectiveLimitsAsync(tenantId.Value, ct);
        var currentCount = await _tenantRepo.GetTenantUserCountAsync(tenantId.Value, ct);

        if (currentCount >= maxUsers)
            throw new TenantLimitExceededException("MaxUsers", currentCount, maxUsers);
    }

    public async Task EnsurePatientLimitAsync(CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null || _currentUser.IsPlatformAdmin) return;

        var (_, maxPatients, _) = await GetEffectiveLimitsAsync(tenantId.Value, ct);
        var currentCount = await _tenantRepo.GetTenantPatientCountAsync(tenantId.Value, ct);

        if (currentCount >= maxPatients)
            throw new TenantLimitExceededException("MaxPatientsPerMonth", currentCount, maxPatients);
    }

    public async Task EnsureStorageLimitAsync(long additionalBytes, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null || _currentUser.IsPlatformAdmin) return;

        var (_, _, storageLimitMb) = await GetEffectiveLimitsAsync(tenantId.Value, ct);
        var usage = await _tenantRepo.GetCurrentUsageAsync(tenantId.Value, ct);
        var currentMb = usage?.StorageUsedMb ?? 0;
        var additionalMb = additionalBytes / (1024 * 1024);

        if (currentMb + additionalMb > storageLimitMb)
            throw new TenantLimitExceededException("StorageLimitMb", (int)currentMb, (int)storageLimitMb);
    }

    public async Task EnsureFeatureEnabledAsync(string featureCode, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null || _currentUser.IsPlatformAdmin) return;

        var enabledFeatures = await _pricingRepo.GetTenantFeatureCodesAsync(tenantId.Value, ct);
        if (!enabledFeatures.Contains(featureCode))
            throw new FeatureNotEnabledException(featureCode);
    }

    /// <summary>
    /// Gets effective limits: PlanDefinition limits take priority, falls back to Tenant defaults.
    /// </summary>
    private async Task<(int MaxUsers, int MaxPatients, long StorageLimitMb)> GetEffectiveLimitsAsync(
        Guid tenantId, CancellationToken ct)
    {
        var subscription = await _tenantRepo.GetActiveSubscriptionAsync(tenantId, ct);
        if (subscription != null)
        {
            var planDef = await _pricingRepo.GetPlanByTypeAsync(subscription.Plan, ct);
            if (planDef != null)
                return (planDef.MaxUsers, planDef.MaxPatientsPerMonth, planDef.StorageLimitMb);
        }

        // Fallback to tenant-level defaults
        var tenant = await _tenantRepo.GetByIdAsync(tenantId, ct);
        if (tenant != null)
            return (tenant.MaxUsers, tenant.MaxPatientsPerMonth, tenant.StorageLimitMb);

        return (5, 50, 1024); // Hard defaults
    }
}
