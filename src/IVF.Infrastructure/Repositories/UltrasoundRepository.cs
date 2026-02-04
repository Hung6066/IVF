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
