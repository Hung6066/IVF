using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class PermissionDefinitionRepository : IPermissionDefinitionRepository
{
    private readonly IvfDbContext _context;

    public PermissionDefinitionRepository(IvfDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PermissionDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.PermissionDefinitions
            .OrderBy(p => p.GroupSortOrder)
            .ThenBy(p => p.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PermissionDefinition>> GetActiveAsync(CancellationToken ct = default)
    {
        return await _context.PermissionDefinitions
            .Where(p => p.IsActive)
            .OrderBy(p => p.GroupSortOrder)
            .ThenBy(p => p.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<PermissionDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.PermissionDefinitions.FindAsync([id], ct);
    }

    public async Task<PermissionDefinition?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _context.PermissionDefinitions
            .FirstOrDefaultAsync(p => p.Code == code, ct);
    }

    public async Task AddAsync(PermissionDefinition entity, CancellationToken ct = default)
    {
        await _context.PermissionDefinitions.AddAsync(entity, ct);
    }

    public async Task AddRangeAsync(IEnumerable<PermissionDefinition> entities, CancellationToken ct = default)
    {
        await _context.PermissionDefinitions.AddRangeAsync(entities, ct);
    }

    public async Task UpdateAsync(PermissionDefinition entity, CancellationToken ct = default)
    {
        _context.PermissionDefinitions.Update(entity);
        await Task.CompletedTask;
    }

    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        return await _context.PermissionDefinitions.AnyAsync(ct);
    }
}
