using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class CycleFeeRepository : ICycleFeeRepository
{
    private readonly IvfDbContext _context;
    public CycleFeeRepository(IvfDbContext context) => _context = context;

    public async Task<CycleFee?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.CycleFees
            .Include(f => f.Cycle)
            .Include(f => f.Patient)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<CycleFee>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.CycleFees
            .AsNoTracking()
            .Include(f => f.Patient)
            .Where(f => f.CycleId == cycleId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> HasFeeForTypeAsync(Guid cycleId, string feeType, CancellationToken ct = default)
        => await _context.CycleFees
            .AnyAsync(f => f.CycleId == cycleId && f.FeeType == feeType && f.IsOneTimePerCycle, ct);

    public async Task<CycleFee> AddAsync(CycleFee fee, CancellationToken ct = default)
    {
        await _context.CycleFees.AddAsync(fee, ct);
        return fee;
    }

    public async Task UpdateAsync(CycleFee fee, CancellationToken ct = default)
    {
        _context.CycleFees.Update(fee);
        await Task.CompletedTask;
    }
}
