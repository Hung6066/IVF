using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class OocyteSampleRepository : IOocyteSampleRepository
{
    private readonly IvfDbContext _context;
    public OocyteSampleRepository(IvfDbContext context) => _context = context;

    public async Task<OocyteSample?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.OocyteSamples.Include(s => s.Donor).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<OocyteSample>> GetByDonorIdAsync(Guid donorId, CancellationToken ct = default)
        => await _context.OocyteSamples.Where(s => s.DonorId == donorId).OrderByDescending(s => s.CollectionDate).ToListAsync(ct);

    public async Task<IReadOnlyList<OocyteSample>> GetAvailableAsync(CancellationToken ct = default)
        => await _context.OocyteSamples.Include(s => s.Donor).Where(s => s.IsAvailable).OrderByDescending(s => s.CollectionDate).ToListAsync(ct);

    public async Task<OocyteSample> AddAsync(OocyteSample sample, CancellationToken ct = default)
    { await _context.OocyteSamples.AddAsync(sample, ct); return sample; }

    public Task UpdateAsync(OocyteSample sample, CancellationToken ct = default)
    { _context.OocyteSamples.Update(sample); return Task.CompletedTask; }

    public async Task<string> GenerateCodeAsync(CancellationToken ct = default)
    {
        var prefix = $"OC-{DateTime.Now:yyyyMMdd}-";
        return await CodeGenerator.NextAsync(_context,
            () => _context.OocyteSamples.IgnoreQueryFilters()
                .Where(o => o.SampleCode.StartsWith(prefix))
                .OrderByDescending(o => o.SampleCode)
                .Select(o => o.SampleCode)
                .FirstOrDefaultAsync(ct),
            prefix, padding: 4, ct);
    }
}
