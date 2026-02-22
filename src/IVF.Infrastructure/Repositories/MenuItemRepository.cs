using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class MenuItemRepository : IMenuItemRepository
{
    private readonly IvfDbContext _context;

    public MenuItemRepository(IvfDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MenuItem>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.MenuItems
            .OrderBy(m => m.Section ?? "")
            .ThenBy(m => m.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MenuItem>> GetActiveAsync(CancellationToken ct = default)
    {
        return await _context.MenuItems
            .Where(m => m.IsActive)
            .OrderBy(m => m.Section ?? "")
            .ThenBy(m => m.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.MenuItems.FindAsync([id], ct);
    }

    public async Task<MenuItem?> GetByRouteAsync(string route, CancellationToken ct = default)
    {
        return await _context.MenuItems
            .FirstOrDefaultAsync(m => m.Route == route, ct);
    }

    public async Task AddAsync(MenuItem item, CancellationToken ct = default)
    {
        await _context.MenuItems.AddAsync(item, ct);
    }

    public async Task AddRangeAsync(IEnumerable<MenuItem> items, CancellationToken ct = default)
    {
        await _context.MenuItems.AddRangeAsync(items, ct);
    }

    public async Task UpdateAsync(MenuItem item, CancellationToken ct = default)
    {
        _context.MenuItems.Update(item);
        await Task.CompletedTask;
    }

    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        return await _context.MenuItems.AnyAsync(ct);
    }
}
