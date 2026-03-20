using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class EmbryoFreezingContractRepository : IEmbryoFreezingContractRepository
{
    private readonly IvfDbContext _context;
    public EmbryoFreezingContractRepository(IvfDbContext context) => _context = context;

    public async Task<EmbryoFreezingContract?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.EmbryoFreezingContracts
            .Include(c => c.Cycle)
            .Include(c => c.Patient)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<EmbryoFreezingContract>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.EmbryoFreezingContracts
            .Where(c => c.CycleId == cycleId)
            .OrderByDescending(c => c.ContractDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EmbryoFreezingContract>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _context.EmbryoFreezingContracts
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.ContractDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EmbryoFreezingContract>> GetExpiringAsync(DateTime before, CancellationToken ct = default)
        => await _context.EmbryoFreezingContracts
            .Include(c => c.Patient)
            .Where(c => c.Status == "Active" && c.StorageEndDate <= before)
            .OrderBy(c => c.StorageEndDate)
            .ToListAsync(ct);

    public async Task<EmbryoFreezingContract> AddAsync(EmbryoFreezingContract contract, CancellationToken ct = default)
    {
        await _context.EmbryoFreezingContracts.AddAsync(contract, ct);
        return contract;
    }

    public Task UpdateAsync(EmbryoFreezingContract contract, CancellationToken ct = default)
    {
        _context.EmbryoFreezingContracts.Update(contract);
        return Task.CompletedTask;
    }

    public async Task<string> GenerateContractNumberAsync(CancellationToken ct = default)
    {
        var count = await _context.EmbryoFreezingContracts.CountAsync(ct);
        return $"HĐ-TRU-{DateTime.Now:yyyyMM}-{count + 1:D4}";
    }
}
