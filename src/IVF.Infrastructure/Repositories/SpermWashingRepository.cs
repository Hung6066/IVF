using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class SpermWashingRepository : ISpermWashingRepository
{
    private readonly IvfDbContext _context;

    public SpermWashingRepository(IvfDbContext context)
    {
        _context = context;
    }

    public async Task<SpermWashing?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.SpermWashings
            .Include(w => w.Patient)
            .Include(w => w.Cycle)
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<IReadOnlyList<SpermWashing>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
    {
        return await _context.SpermWashings
            .Include(w => w.Patient)
            .Where(w => w.CycleId == cycleId)
            .OrderByDescending(w => w.WashDate)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<SpermWashing> Items, int Total)> SearchAsync(
        string? method, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.SpermWashings
            .Include(w => w.Patient)
            .Include(w => w.Cycle)
            .AsQueryable();

        if (!string.IsNullOrEmpty(method))
            q = q.Where(w => w.Method == method);

        if (fromDate.HasValue)
            q = q.Where(w => w.WashDate >= fromDate.Value);

        if (toDate.HasValue)
            q = q.Where(w => w.WashDate <= toDate.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(w => w.WashDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<SpermWashing> AddAsync(SpermWashing washing, CancellationToken ct = default)
    {
        await _context.SpermWashings.AddAsync(washing, ct);
        return washing;
    }

    public Task UpdateAsync(SpermWashing washing, CancellationToken ct = default)
    {
        _context.SpermWashings.Update(washing);
        return Task.CompletedTask;
    }

    public async Task<int> GetCountByDateAsync(DateTime date, CancellationToken ct = default)
    {
        return await _context.SpermWashings
            .CountAsync(w => w.WashDate.Date == date.Date, ct);
    }
}
