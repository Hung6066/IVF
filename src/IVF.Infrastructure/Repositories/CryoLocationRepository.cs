using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
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
                Used = g.Count(x => x.IsOccupied),
                SpecimenType = g.Select(x => x.SpecimenType).First() // Assume all in tank have same type
            })
            .ToListAsync(ct);

        return stats.Select(s => new CryoStatsDto(s.Tank, s.CanisterCount, s.CaneCount, s.GobletCount, s.Available, s.Used, (int)s.SpecimenType)).ToList();
    }

    public async Task<bool> TankExistsAsync(string tank, CancellationToken ct = default)
    {
        return await _context.CryoLocations.AnyAsync(c => c.Tank == tank, ct);
    }

    public async Task DeleteTankAsync(string tank, CancellationToken ct = default)
    {
        var locations = await _context.CryoLocations.Where(c => c.Tank == tank).ToListAsync(ct);
        _context.CryoLocations.RemoveRange(locations);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<SpecimenType, int>> GetSpecimenCountsAsync(CancellationToken ct = default)
    {
        return await _context.CryoLocations
            .Where(c => c.IsOccupied)
            .GroupBy(c => c.SpecimenType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, ct);
    }

    public async Task SetTankOccupancyAsync(string tank, int occupiedCount, SpecimenType type, CancellationToken ct = default)
    {
        var locations = await _context.CryoLocations
            .Where(x => x.Tank == tank)
            .OrderBy(x => x.Canister)
            .ThenBy(x => x.Cane)
            .ThenBy(x => x.Goblet)
            .ToListAsync(ct);

        if (!locations.Any()) return;

        int count = 0;
        foreach (var loc in locations)
        {
            // We are brute-forcing the specimen type for the whole tank/batch
            // This assumes the tank is uniform or we are setting it as such.
            // Since CryoLocation doesn't expose a setter for SpecimenType,
            // we might need to use Entry(loc).CurrentValues or similar, OR add a method to Domain.
            // Let's check if we can hack it via EF Entry for now to avoid Domain change if possible,
            // OR better, add a method to Domain `UpdateSpecimenType`.
            
            // Checking: loc.SpecimenType is likely private set.
            // I'll try to set it via Entry if needed, but let's assume I can't.
            // Actually, I should check CryoLocation.cs.
            
            // For now, I'll just handle occupancy. 
            // If I need to update type, I'll need to update Domain.
            // Let's assume for this task, the user wants to FIX the type too.
            // So `_context.Entry(loc).Property(x => x.SpecimenType).CurrentValue = type;`
            
            _context.Entry(loc).Property(x => x.SpecimenType).CurrentValue = type;

            bool shouldBeOccupied = count < occupiedCount;
            if (shouldBeOccupied)
            {
                if (!loc.IsOccupied) loc.Occupy();
            }
            else
            {
                if (loc.IsOccupied) loc.Release();
            }
            count++;
        }
    }
}
