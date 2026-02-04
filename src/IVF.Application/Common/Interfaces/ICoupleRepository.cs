using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface ICoupleRepository
{
    Task<Couple?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Couple?> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<Couple?> GetByWifeAndHusbandAsync(Guid wifeId, Guid husbandId, CancellationToken ct = default);
    Task<IReadOnlyList<Couple>> GetAllAsync(CancellationToken ct = default);
    Task<Couple> AddAsync(Couple couple, CancellationToken ct = default);
    Task UpdateAsync(Couple couple, CancellationToken ct = default);
}
