using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class SemenAnalysisRepository : ISemenAnalysisRepository
{
    private readonly IvfDbContext _context;
    public SemenAnalysisRepository(IvfDbContext context) => _context = context;

    public async Task<SemenAnalysis?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.SemenAnalyses.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SemenAnalysis>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _context.SemenAnalyses.Where(s => s.PatientId == patientId).OrderByDescending(s => s.AnalysisDate).ToListAsync(ct);

    public async Task<IReadOnlyList<SemenAnalysis>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.SemenAnalyses.Where(s => s.CycleId == cycleId).OrderByDescending(s => s.AnalysisDate).ToListAsync(ct);

    public async Task<SemenAnalysis> AddAsync(SemenAnalysis analysis, CancellationToken ct = default)
    { await _context.SemenAnalyses.AddAsync(analysis, ct); return analysis; }

    public Task UpdateAsync(SemenAnalysis analysis, CancellationToken ct = default)
    { _context.SemenAnalyses.Update(analysis); return Task.CompletedTask; }
}
