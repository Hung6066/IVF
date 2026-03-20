using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IPrescriptionRepository
{
    Task<Prescription?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Prescription?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Prescription>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<Prescription>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<(IReadOnlyList<Prescription> Items, int Total)> SearchAsync(string? query, DateTime? fromDate, DateTime? toDate, string? status, int page, int pageSize, CancellationToken ct = default);
    Task<int> GetCountByDateAsync(DateTime date, CancellationToken ct = default);
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
    Task<Prescription> AddAsync(Prescription prescription, CancellationToken ct = default);
    Task UpdateAsync(Prescription prescription, CancellationToken ct = default);
}
