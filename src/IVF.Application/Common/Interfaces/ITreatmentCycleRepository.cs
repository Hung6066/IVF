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
    // Lab Dashboard
    Task<Dictionary<IVF.Domain.Enums.CyclePhase, int>> GetPhaseCountsAsync(CancellationToken ct = default);
    Task<List<LabScheduleDto>> GetLabScheduleAsync(DateTime date, CancellationToken ct = default);
    Task<IReadOnlyList<TreatmentCycle>> GetActiveCyclesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TreatmentCycle>> GetAllWithDetailsAsync(CancellationToken ct = default);
}

public class LabScheduleDto
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string CycleCode { get; set; } = string.Empty;
    public string Procedure { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
