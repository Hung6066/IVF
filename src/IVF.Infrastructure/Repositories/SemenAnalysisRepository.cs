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

    public async Task<(IReadOnlyList<SemenAnalysis> Items, int Total)> SearchAsync(string? query, DateTime? fromDate, DateTime? toDate, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.SemenAnalyses
            .Include(s => s.Patient)
            .Include(s => s.Cycle)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query))
        {
            q = q.Where(s => s.Patient.FullName.Contains(query) || s.Patient.PatientCode.Contains(query));
        }

        if (fromDate.HasValue)
        {
            q = q.Where(s => s.AnalysisDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            q = q.Where(s => s.AnalysisDate <= toDate.Value);
        }
        
        if (!string.IsNullOrEmpty(status))
        {
             if (status == "Pending")
                 q = q.Where(s => s.Concentration == null);
             else if (status == "Completed")
                 q = q.Where(s => s.Concentration != null);
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(s => s.AnalysisDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
            
        return (items, total);
    }

    public async Task<int> GetCountByDateAsync(DateTime date, CancellationToken ct = default)
    {
        return await _context.SemenAnalyses.CountAsync(s => s.AnalysisDate.Date == date.Date, ct);
    }

    public async Task<decimal?> GetAverageConcentrationAsync(CancellationToken ct = default)
    {
        // AverageAsync can throw if no elements, handle carefully or use AverageAsync on nullable?
        // Actually AverageAsync on decimal? works and returns null if empty? No, standard LINQ behavior.
        // Let's check if any exists first or catch exception?
        // Safe way:
        var concentrations = await _context.SemenAnalyses
            .Where(s => s.Concentration.HasValue)
            .Select(s => s.Concentration!.Value)
            .ToListAsync(ct);
            
        if (!concentrations.Any()) return null;
        return concentrations.Average();
    }

    public async Task<SemenAnalysis> AddAsync(SemenAnalysis analysis, CancellationToken ct = default)
    {
        await _context.SemenAnalyses.AddAsync(analysis, ct);
        return analysis;
    }

    public Task UpdateAsync(SemenAnalysis analysis, CancellationToken ct = default)
    { _context.SemenAnalyses.Update(analysis); return Task.CompletedTask; }
}
