using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class PricingRepository : IPricingRepository
{
    private readonly IvfDbContext _context;

    public PricingRepository(IvfDbContext context) => _context = context;

    public async Task<List<PlanDefinition>> GetActivePlansWithFeaturesAsync(CancellationToken ct = default)
    {
        return await _context.PlanDefinitions
            .Where(p => p.IsActive)
            .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.FeatureDefinition)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<PlanDefinition?> GetPlanByTypeAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        return await _context.PlanDefinitions
            .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.FeatureDefinition)
            .FirstOrDefaultAsync(p => p.Plan == plan, ct);
    }

    public async Task<List<FeatureDefinition>> GetAllFeaturesAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        var query = _context.FeatureDefinitions.AsQueryable();
        if (activeOnly)
            query = query.Where(f => f.IsActive);
        return await query.OrderBy(f => f.SortOrder).ToListAsync(ct);
    }

    public async Task<List<string>> GetTenantFeatureCodesAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _context.TenantFeatures
            .Where(tf => tf.TenantId == tenantId && tf.IsEnabled)
            .Include(tf => tf.FeatureDefinition)
            .Where(tf => tf.FeatureDefinition.IsActive)
            .Select(tf => tf.FeatureDefinition.Code)
            .ToListAsync(ct);
    }

    public async Task SyncTenantFeaturesFromPlanAsync(Guid tenantId, SubscriptionPlan plan, CancellationToken ct = default)
    {
        var planDef = await _context.PlanDefinitions
            .Include(p => p.PlanFeatures)
            .FirstOrDefaultAsync(p => p.Plan == plan, ct);

        if (planDef is null) return;

        // Remove existing tenant features
        var existing = await _context.TenantFeatures
            .Where(tf => tf.TenantId == tenantId)
            .ToListAsync(ct);
        _context.TenantFeatures.RemoveRange(existing);

        // Add features from plan
        foreach (var pf in planDef.PlanFeatures.Where(pf => !pf.IsDeleted))
        {
            await _context.TenantFeatures.AddAsync(
                TenantFeature.Create(tenantId, pf.FeatureDefinitionId), ct);
        }
    }
}
