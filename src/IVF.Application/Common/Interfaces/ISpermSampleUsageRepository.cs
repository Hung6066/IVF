using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface ISpermSampleUsageRepository
{
    Task<SpermSampleUsage?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SpermSampleUsage>> GetBySampleIdAsync(Guid sampleId, CancellationToken ct = default);
    Task<IReadOnlyList<SpermSampleUsage>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<SpermSampleUsage> AddAsync(SpermSampleUsage usage, CancellationToken ct = default);
    Task UpdateAsync(SpermSampleUsage usage, CancellationToken ct = default);
}
