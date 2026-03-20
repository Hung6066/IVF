using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IConsultationRepository
{
    Task<Consultation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Consultation>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<Consultation>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<(IReadOnlyList<Consultation> Items, int Total)> SearchAsync(string? query, string? status, string? type, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken ct = default);
    Task<Consultation> AddAsync(Consultation consultation, CancellationToken ct = default);
    Task UpdateAsync(Consultation consultation, CancellationToken ct = default);
}
