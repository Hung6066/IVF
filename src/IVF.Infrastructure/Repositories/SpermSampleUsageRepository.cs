using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class SpermSampleUsageRepository : ISpermSampleUsageRepository
{
    private readonly IvfDbContext _context;
    public SpermSampleUsageRepository(IvfDbContext context) => _context = context;

    public async Task<SpermSampleUsage?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.SpermSampleUsages
            .Include(u => u.SpermSample)
            .Include(u => u.Cycle)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<SpermSampleUsage>> GetBySampleIdAsync(Guid sampleId, CancellationToken ct = default)
        => await _context.SpermSampleUsages
            .Where(u => u.SpermSampleId == sampleId)
            .OrderByDescending(u => u.UsageDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SpermSampleUsage>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.SpermSampleUsages
            .Include(u => u.SpermSample)
            .Where(u => u.CycleId == cycleId)
            .OrderByDescending(u => u.UsageDate)
            .ToListAsync(ct);

    public async Task<SpermSampleUsage> AddAsync(SpermSampleUsage usage, CancellationToken ct = default)
    {
        await _context.SpermSampleUsages.AddAsync(usage, ct);
        return usage;
    }

    public Task UpdateAsync(SpermSampleUsage usage, CancellationToken ct = default)
    {
        _context.SpermSampleUsages.Update(usage);
        return Task.CompletedTask;
    }
}
