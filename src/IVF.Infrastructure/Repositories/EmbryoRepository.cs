using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class EmbryoRepository : IEmbryoRepository
{
    private readonly IvfDbContext _context;

    public EmbryoRepository(IvfDbContext context) => _context = context;

    public async Task<Embryo?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Embryos.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Embryo>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.Embryos
            .AsNoTracking()
            .Where(e => e.CycleId == cycleId)
            .OrderBy(e => e.EmbryoNumber)
            .ToListAsync(ct);

    public async Task<Embryo> AddAsync(Embryo embryo, CancellationToken ct = default)
    {
        await _context.Embryos.AddAsync(embryo, ct);
        return embryo;
    }

    public Task UpdateAsync(Embryo embryo, CancellationToken ct = default)
    {
        _context.Embryos.Update(embryo);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Embryo embryo, CancellationToken ct = default)
    {
        _context.Embryos.Remove(embryo);
        return Task.CompletedTask;
    }

    public async Task<int> GetNextNumberForCycleAsync(Guid cycleId, CancellationToken ct = default)
    {
        var max = await _context.Embryos
            .Where(e => e.CycleId == cycleId)
            .MaxAsync(e => (int?)e.EmbryoNumber, ct) ?? 0;
        return max + 1;
    }

    public async Task<IReadOnlyList<Embryo>> GetActiveAsync(CancellationToken ct = default)
        => await _context.Embryos
            .Include(e => e.Cycle).ThenInclude(c => c.Couple).ThenInclude(cp => cp.Wife)
            .Include(e => e.CryoLocation) // Added for location tank name
            .Where(e => e.Status == EmbryoStatus.Developing || e.Status == EmbryoStatus.Frozen)
            .OrderBy(e => e.CycleId)
            .ThenBy(e => e.EmbryoNumber)
            .ToListAsync(ct);
}
