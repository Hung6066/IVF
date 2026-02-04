using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface ITreatmentCycleRepository
{
    Task<TreatmentCycle?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TreatmentCycle?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TreatmentCycle>> GetByCoupleIdAsync(Guid coupleId, CancellationToken ct = default);
    Task<TreatmentCycle> AddAsync(TreatmentCycle cycle, CancellationToken ct = default);
    Task UpdateAsync(TreatmentCycle cycle, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);
    // Reporting
    Task<int> GetActiveCountAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetOutcomeStatsAsync(int year, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetMethodDistributionAsync(int year, CancellationToken ct = default);
}
