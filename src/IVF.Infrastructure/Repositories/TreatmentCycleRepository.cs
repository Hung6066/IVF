using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class TreatmentCycleRepository : ITreatmentCycleRepository
{
    private readonly IvfDbContext _context;

    public TreatmentCycleRepository(IvfDbContext context) => _context = context;

    public async Task<TreatmentCycle?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.TreatmentCycles.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<TreatmentCycle?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _context.TreatmentCycles
            .Include(c => c.Couple).ThenInclude(c => c.Wife)
            .Include(c => c.Couple).ThenInclude(c => c.Husband)
            .Include(c => c.Ultrasounds)
            .Include(c => c.Embryos)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<TreatmentCycle>> GetByCoupleIdAsync(Guid coupleId, CancellationToken ct = default)
        => await _context.TreatmentCycles
            .Where(c => c.CoupleId == coupleId)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(ct);

    public async Task<TreatmentCycle> AddAsync(TreatmentCycle cycle, CancellationToken ct = default)
    {
        await _context.TreatmentCycles.AddAsync(cycle, ct);
        return cycle;
    }

    public Task UpdateAsync(TreatmentCycle cycle, CancellationToken ct = default)
    {
        _context.TreatmentCycles.Update(cycle);
        return Task.CompletedTask;
    }

    public async Task<string> GenerateCodeAsync(CancellationToken ct = default)
    {
        var count = await _context.TreatmentCycles.CountAsync(ct);
        return $"CK-{DateTime.Now:yyyy}-{count + 1:D4}";
    }

    public async Task<int> GetActiveCountAsync(CancellationToken ct = default)
        => await _context.TreatmentCycles.CountAsync(c => c.Outcome == Domain.Enums.CycleOutcome.Ongoing, ct);

    public async Task<Dictionary<string, int>> GetOutcomeStatsAsync(int year, CancellationToken ct = default)
    {
        var cycles = await _context.TreatmentCycles
            .Where(c => c.StartDate.Year == year && c.Outcome != Domain.Enums.CycleOutcome.Ongoing)
            .GroupBy(c => c.Outcome)
            .Select(g => new { Outcome = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);
        return cycles.ToDictionary(x => x.Outcome, x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetMethodDistributionAsync(int year, CancellationToken ct = default)
    {
        var cycles = await _context.TreatmentCycles
            .Where(c => c.StartDate.Year == year)
            .GroupBy(c => c.Method)
            .Select(g => new { Method = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);
        return cycles.ToDictionary(x => x.Method, x => x.Count);
    }
}
