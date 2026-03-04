using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly IvfDbContext _context;

    public TenantRepository(IvfDbContext context) => _context = context;

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await _context.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => await _context.Tenants.AnyAsync(t => t.Slug == slug, ct);

    public async Task<(IReadOnlyList<Tenant> Items, int TotalCount)> GetAllAsync(
        int page, int pageSize, string? search, TenantStatus? status, CancellationToken ct = default)
    {
        var query = _context.Tenants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Name.Contains(search) || t.Slug.Contains(search));

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default)
        => await _context.Tenants.AddAsync(tenant, ct);

    public async Task<TenantSubscription?> GetActiveSubscriptionAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active, ct);

    public async Task AddSubscriptionAsync(TenantSubscription subscription, CancellationToken ct = default)
        => await _context.TenantSubscriptions.AddAsync(subscription, ct);

    public async Task<TenantUsageRecord?> GetCurrentUsageAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _context.TenantUsageRecords
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Year == now.Year && u.Month == now.Month, ct);
    }

    public async Task AddUsageRecordAsync(TenantUsageRecord usage, CancellationToken ct = default)
        => await _context.TenantUsageRecords.AddAsync(usage, ct);

    public async Task<int> GetTenantUserCountAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Users.IgnoreQueryFilters()
            .CountAsync(u => u.TenantId == tenantId && !u.IsDeleted && u.IsActive, ct);

    public async Task<int> GetTenantPatientCountAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Patients.IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == tenantId && !p.IsDeleted, ct);

    public async Task<decimal> GetTotalMonthlyRevenueAsync(CancellationToken ct = default)
        => await _context.TenantSubscriptions.AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.Active)
            .SumAsync(s => s.MonthlyPrice, ct);

    public async Task<List<TenantUsageRecord>> GetAllCurrentUsagesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _context.TenantUsageRecords.AsNoTracking()
            .Where(u => u.Year == now.Year && u.Month == now.Month)
            .ToListAsync(ct);
    }

    public async Task<SubscriptionPlan?> GetTenantActiveSubscriptionPlanAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.TenantSubscriptions
            .Where(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active)
            .Select(s => (SubscriptionPlan?)s.Plan)
            .FirstOrDefaultAsync(ct);

    public async Task<List<Tenant>> GetAllTenantsRawAsync(CancellationToken ct = default)
        => await _context.Tenants.AsNoTracking().ToListAsync(ct);
}
