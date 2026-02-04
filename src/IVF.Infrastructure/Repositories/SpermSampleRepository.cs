using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class SpermSampleRepository : ISpermSampleRepository
{
    private readonly IvfDbContext _context;
    public SpermSampleRepository(IvfDbContext context) => _context = context;

    public async Task<SpermSample?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.SpermSamples.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SpermSample>> GetByDonorIdAsync(Guid donorId, CancellationToken ct = default)
        => await _context.SpermSamples.Where(s => s.DonorId == donorId).OrderByDescending(s => s.CollectionDate).ToListAsync(ct);

    public async Task<IReadOnlyList<SpermSample>> GetAvailableAsync(CancellationToken ct = default)
        => await _context.SpermSamples.Include(s => s.Donor).Where(s => s.IsAvailable).ToListAsync(ct);

    public async Task<SpermSample> AddAsync(SpermSample sample, CancellationToken ct = default)
    { await _context.SpermSamples.AddAsync(sample, ct); return sample; }

    public Task UpdateAsync(SpermSample sample, CancellationToken ct = default)
    { _context.SpermSamples.Update(sample); return Task.CompletedTask; }

    public async Task<string> GenerateCodeAsync(CancellationToken ct = default)
    {
        var count = await _context.SpermSamples.CountAsync(ct);
        return $"SP-{DateTime.Now:yyyyMMdd}-{count + 1:D4}";
    }
}
