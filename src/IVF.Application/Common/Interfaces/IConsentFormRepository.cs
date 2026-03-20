using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IConsentFormRepository
{
    Task<ConsentForm?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentForm>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentForm>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentForm>> GetPendingByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<(IReadOnlyList<ConsentForm> Items, int Total)> SearchAsync(string? query, string? status, string? consentType, int page, int pageSize, CancellationToken ct = default);
    Task<bool> HasValidConsentAsync(Guid patientId, string consentType, Guid? cycleId, CancellationToken ct = default);
    Task<ConsentForm> AddAsync(ConsentForm consent, CancellationToken ct = default);
    Task UpdateAsync(ConsentForm consent, CancellationToken ct = default);
}
