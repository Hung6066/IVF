using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IUltrasoundRepository
{
    Task<Ultrasound?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Ultrasound>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<Ultrasound> AddAsync(Ultrasound ultrasound, CancellationToken ct = default);
    Task UpdateAsync(Ultrasound ultrasound, CancellationToken ct = default);
}
