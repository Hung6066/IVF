using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IMedicationAdministrationRepository
{
    Task<MedicationAdministration?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MedicationAdministration>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<IReadOnlyList<MedicationAdministration>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<MedicationAdministration>> GetTriggerShotsByCycleAsync(Guid cycleId, CancellationToken ct = default);
    Task<(IReadOnlyList<MedicationAdministration> Items, int Total)> SearchAsync(string? query, Guid? cycleId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken ct = default);
    Task<MedicationAdministration> AddAsync(MedicationAdministration med, CancellationToken ct = default);
    Task UpdateAsync(MedicationAdministration med, CancellationToken ct = default);
}
