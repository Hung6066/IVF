using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class ServiceCatalogRepository : IServiceCatalogRepository
{
    private readonly IvfDbContext _context;

    public ServiceCatalogRepository(IvfDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceCatalog?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<ServiceCatalog>().FindAsync([id], ct);
    }

    public async Task<ServiceCatalog?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _context.Set<ServiceCatalog>()
            .FirstOrDefaultAsync(s => s.Code == code, ct);
    }

    public async Task<IReadOnlyList<ServiceCatalog>> GetAllAsync(ServiceCategory? category = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _context.Set<ServiceCatalog>().AsQueryable();

        if (category.HasValue)
            query = query.Where(s => s.Category == category.Value);

        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        return await query.OrderBy(s => s.Category).ThenBy(s => s.Code).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ServiceCatalog>> SearchAsync(string? searchQuery, ServiceCategory? category = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var query = _context.Set<ServiceCatalog>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var pattern = $"%{searchQuery}%";
            query = query.Where(s => EF.Functions.ILike(s.Code, pattern) || EF.Functions.ILike(s.Name, pattern));
        }

        if (category.HasValue)
            query = query.Where(s => s.Category == category.Value);

        return await query
            .OrderBy(s => s.Category).ThenBy(s => s.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(string? searchQuery, ServiceCategory? category = null, CancellationToken ct = default)
    {
        var query = _context.Set<ServiceCatalog>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var pattern = $"%{searchQuery}%";
            query = query.Where(s => EF.Functions.ILike(s.Code, pattern) || EF.Functions.ILike(s.Name, pattern));
        }

        if (category.HasValue)
            query = query.Where(s => s.Category == category.Value);

        return await query.CountAsync(ct);
    }

    public async Task AddAsync(ServiceCatalog service, CancellationToken ct = default)
    {
        await _context.Set<ServiceCatalog>().AddAsync(service, ct);
    }

    public async Task UpdateAsync(ServiceCatalog service, CancellationToken ct = default)
    {
        _context.Set<ServiceCatalog>().Update(service);
        await Task.CompletedTask;
    }
}
