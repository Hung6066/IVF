using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class CoupleRepository : ICoupleRepository
{
    private readonly IvfDbContext _context;

    public CoupleRepository(IvfDbContext context) => _context = context;

    public async Task<Couple?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Couples
            .AsNoTracking()
            .Include(c => c.Wife)
            .Include(c => c.Husband)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Couple?> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _context.Couples
            .AsNoTracking()
            .Include(c => c.Wife)
            .Include(c => c.Husband)
            .FirstOrDefaultAsync(c => c.WifeId == patientId || c.HusbandId == patientId, ct);

    public async Task<Couple?> GetByWifeAndHusbandAsync(Guid wifeId, Guid husbandId, CancellationToken ct = default)
        => await _context.Couples
            .FirstOrDefaultAsync(c => c.WifeId == wifeId && c.HusbandId == husbandId, ct);

    public async Task<IReadOnlyList<Couple>> GetAllAsync(CancellationToken ct = default)
        => await _context.Couples
            .AsNoTracking()
            .Include(c => c.Wife)
            .Include(c => c.Husband)
            .ToListAsync(ct);

    public async Task<Couple> AddAsync(Couple couple, CancellationToken ct = default)
    {
        await _context.Couples.AddAsync(couple, ct);
        return couple;
    }

    public Task UpdateAsync(Couple couple, CancellationToken ct = default)
    {
        _context.Couples.Update(couple);
        return Task.CompletedTask;
    }
}
