using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface ISemenAnalysisRepository
{
    Task<SemenAnalysis?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SemenAnalysis>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<SemenAnalysis>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<(IReadOnlyList<SemenAnalysis> Items, int Total)> SearchAsync(string? query, DateTime? fromDate, DateTime? toDate, string? status, int page, int pageSize, CancellationToken ct = default);
    Task<int> GetCountByDateAsync(DateTime date, CancellationToken ct = default);
    Task<decimal?> GetAverageConcentrationAsync(CancellationToken ct = default);
    Task<SemenAnalysis> AddAsync(SemenAnalysis analysis, CancellationToken ct = default);
    Task UpdateAsync(SemenAnalysis analysis, CancellationToken ct = default);
}
