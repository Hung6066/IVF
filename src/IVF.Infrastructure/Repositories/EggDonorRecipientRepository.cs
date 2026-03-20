using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class EggDonorRecipientRepository : IEggDonorRecipientRepository
{
    private readonly IvfDbContext _context;
    public EggDonorRecipientRepository(IvfDbContext context) => _context = context;

    public async Task<EggDonorRecipient?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _context.EggDonorRecipients
            .Include(m => m.EggDonor)
            .Include(m => m.RecipientCouple)
            .Include(m => m.Cycle)
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<EggDonorRecipient>> GetByDonorAsync(Guid eggDonorId, Guid tenantId, CancellationToken ct = default)
        => await _context.EggDonorRecipients
            .AsNoTracking()
            .Include(m => m.RecipientCouple)
            .Include(m => m.Cycle)
            .Where(m => m.EggDonorId == eggDonorId && m.TenantId == tenantId)
            .OrderByDescending(m => m.MatchedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EggDonorRecipient>> GetByRecipientCoupleAsync(Guid coupleId, Guid tenantId, CancellationToken ct = default)
        => await _context.EggDonorRecipients
            .AsNoTracking()
            .Include(m => m.EggDonor)
            .Include(m => m.Cycle)
            .Where(m => m.RecipientCoupleId == coupleId && m.TenantId == tenantId)
            .OrderByDescending(m => m.MatchedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EggDonorRecipient>> GetByCycleAsync(Guid cycleId, Guid tenantId, CancellationToken ct = default)
        => await _context.EggDonorRecipients
            .AsNoTracking()
            .Include(m => m.EggDonor)
            .Where(m => m.CycleId == cycleId && m.TenantId == tenantId)
            .ToListAsync(ct);

    public async Task<bool> MatchExistsAsync(Guid eggDonorId, Guid coupleId, Guid tenantId, CancellationToken ct = default)
        => await _context.EggDonorRecipients
            .AnyAsync(m => m.EggDonorId == eggDonorId && m.RecipientCoupleId == coupleId && m.TenantId == tenantId
                && m.Status != MatchStatus.Cancelled, ct);

    public async Task AddAsync(EggDonorRecipient match, CancellationToken ct = default)
        => await _context.EggDonorRecipients.AddAsync(match, ct);

    public Task UpdateAsync(EggDonorRecipient match, CancellationToken ct = default)
    {
        _context.EggDonorRecipients.Update(match);
        return Task.CompletedTask;
    }
}
