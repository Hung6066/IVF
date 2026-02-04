using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface ISpermSampleRepository
{
    Task<SpermSample?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SpermSample>> GetByDonorIdAsync(Guid donorId, CancellationToken ct = default);
    Task<IReadOnlyList<SpermSample>> GetAvailableAsync(CancellationToken ct = default);
    Task<SpermSample> AddAsync(SpermSample sample, CancellationToken ct = default);
    Task UpdateAsync(SpermSample sample, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);
}
