using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class DrugCatalogRepository : IDrugCatalogRepository
{
    private readonly IvfDbContext _context;
    public DrugCatalogRepository(IvfDbContext context) => _context = context;

    public async Task<DrugCatalog?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _context.DrugCatalog
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, ct);

    public async Task<DrugCatalog?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct = default)
        => await _context.DrugCatalog
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Code == code && d.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<DrugCatalog>> GetByCategoryAsync(DrugCategory category, Guid tenantId, CancellationToken ct = default)
        => await _context.DrugCatalog
            .AsNoTracking()
            .Where(d => d.Category == category && d.TenantId == tenantId)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DrugCatalog>> GetActiveAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.DrugCatalog
            .AsNoTracking()
            .Where(d => d.IsActive && d.TenantId == tenantId)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<DrugCatalog> Items, int Total)> SearchAsync(string? query, DrugCategory? category, bool? isActive, int page, int pageSize, Guid tenantId, CancellationToken ct = default)
    {
        var q = _context.DrugCatalog.AsNoTracking().Where(d => d.TenantId == tenantId);

        if (!string.IsNullOrEmpty(query))
            q = q.Where(d => d.Name.Contains(query) || d.GenericName.Contains(query) || d.Code.Contains(query));

        if (category.HasValue)
            q = q.Where(d => d.Category == category.Value);

        if (isActive.HasValue)
            q = q.Where(d => d.IsActive == isActive.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(d => d.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<bool> CodeExistsAsync(string code, Guid tenantId, CancellationToken ct = default)
        => await _context.DrugCatalog.AnyAsync(d => d.Code == code && d.TenantId == tenantId, ct);

    public async Task AddAsync(DrugCatalog drug, CancellationToken ct = default)
        => await _context.DrugCatalog.AddAsync(drug, ct);

    public Task UpdateAsync(DrugCatalog drug, CancellationToken ct = default)
    {
        _context.DrugCatalog.Update(drug);
        return Task.CompletedTask;
    }
}
