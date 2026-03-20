using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class EggDonorRepository : IEggDonorRepository
{
    private readonly IvfDbContext _context;
    public EggDonorRepository(IvfDbContext context) => _context = context;

    public async Task<EggDonor?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.EggDonors.Include(d => d.Patient).FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<EggDonor?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _context.EggDonors.Include(d => d.Patient).FirstOrDefaultAsync(d => d.DonorCode == code, ct);

    public async Task<(IReadOnlyList<EggDonor> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.EggDonors.Include(d => d.Patient).AsQueryable();
        if (!string.IsNullOrEmpty(query))
            q = q.Where(d => d.DonorCode.Contains(query) || d.Patient.FullName.Contains(query));
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(d => d.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<EggDonor> AddAsync(EggDonor donor, CancellationToken ct = default)
    { await _context.EggDonors.AddAsync(donor, ct); return donor; }

    public Task UpdateAsync(EggDonor donor, CancellationToken ct = default)
    { _context.EggDonors.Update(donor); return Task.CompletedTask; }

    public async Task<string> GenerateCodeAsync(CancellationToken ct = default)
    {
        var count = await _context.EggDonors.CountAsync(ct);
        return $"NHN-{DateTime.Now:yyyy}-{count + 1:D4}";
    }
}
