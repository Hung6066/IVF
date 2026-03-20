using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class EndometriumScanRepository : IEndometriumScanRepository
{
    private readonly IvfDbContext _context;
    public EndometriumScanRepository(IvfDbContext context) => _context = context;

    public async Task<EndometriumScan?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.EndometriumScans.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<EndometriumScan>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.EndometriumScans
            .Where(s => s.CycleId == cycleId)
            .OrderBy(s => s.ScanDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EndometriumScan>> GetByFetProtocolIdAsync(Guid fetProtocolId, CancellationToken ct = default)
        => await _context.EndometriumScans
            .Where(s => s.FetProtocolId == fetProtocolId)
            .OrderBy(s => s.ScanDate)
            .ToListAsync(ct);

    public async Task<EndometriumScan> AddAsync(EndometriumScan scan, CancellationToken ct = default)
    {
        await _context.EndometriumScans.AddAsync(scan, ct);
        return scan;
    }

    public Task UpdateAsync(EndometriumScan scan, CancellationToken ct = default)
    {
        _context.EndometriumScans.Update(scan);
        return Task.CompletedTask;
    }
}
