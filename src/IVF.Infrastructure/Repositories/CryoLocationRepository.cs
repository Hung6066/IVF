using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

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

    public async Task<IReadOnlyList<CryoStatsDto>> GetStorageStatsAsync(CancellationToken ct = default)
    {
        var stats = await _context.CryoLocations
            .GroupBy(c => c.Tank)
            .Select(g => new
            {
                Tank = g.Key,
                CanisterCount = g.Select(x => x.Canister).Distinct().Count(),
                CaneCount = g.Select(x => x.Cane).Distinct().Count(),
                GobletCount = g.Select(x => x.Goblet).Distinct().Count(),
                Available = g.Count(x => !x.IsOccupied),
                Used = g.Count(x => x.IsOccupied)
            })
            .ToListAsync(ct);

        return stats.Select(s => new CryoStatsDto(s.Tank, s.CanisterCount, s.CaneCount, s.GobletCount, s.Available, s.Used)).ToList();
    }
}
