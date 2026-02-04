using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class UltrasoundRepository : IUltrasoundRepository
{
    private readonly IvfDbContext _context;

    public UltrasoundRepository(IvfDbContext context) => _context = context;

    public async Task<Ultrasound?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Ultrasounds.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<Ultrasound>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.Ultrasounds
            .Where(u => u.CycleId == cycleId)
            .OrderByDescending(u => u.ExamDate)
            .ToListAsync(ct);

    public async Task<Ultrasound> AddAsync(Ultrasound ultrasound, CancellationToken ct = default)
    {
        await _context.Ultrasounds.AddAsync(ultrasound, ct);
        return ultrasound;
    }

    public Task UpdateAsync(Ultrasound ultrasound, CancellationToken ct = default)
    {
        _context.Ultrasounds.Update(ultrasound);
        return Task.CompletedTask;
    }
}

public class EmbryoRepository : IEmbryoRepository
{
    private readonly IvfDbContext _context;

    public EmbryoRepository(IvfDbContext context) => _context = context;

    public async Task<Embryo?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Embryos.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Embryo>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.Embryos
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

    public async Task<int> GetNextNumberForCycleAsync(Guid cycleId, CancellationToken ct = default)
    {
        var max = await _context.Embryos
            .Where(e => e.CycleId == cycleId)
            .MaxAsync(e => (int?)e.EmbryoNumber, ct) ?? 0;
        return max + 1;
    }
}

public class CryoLocationRepository : ICryoLocationRepository
{
    private readonly IvfDbContext _context;

    public CryoLocationRepository(IvfDbContext context) => _context = context;

    public async Task<CryoLocation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.CryoLocations.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<CryoLocation>> GetAvailableAsync(CancellationToken ct = default)
        => await _context.CryoLocations
            .Where(c => !c.IsOccupied)
            .OrderBy(c => c.Tank)
            .ThenBy(c => c.Canister)
            .ThenBy(c => c.Cane)
            .ToListAsync(ct);

    public async Task<CryoLocation> AddAsync(CryoLocation location, CancellationToken ct = default)
    {
        await _context.CryoLocations.AddAsync(location, ct);
        return location;
    }

    public Task UpdateAsync(CryoLocation location, CancellationToken ct = default)
    {
        _context.CryoLocations.Update(location);
        return Task.CompletedTask;
    }
}
