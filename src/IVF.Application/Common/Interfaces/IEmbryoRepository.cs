using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IEmbryoRepository
{
    Task<Embryo?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Embryo>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<Embryo> AddAsync(Embryo embryo, CancellationToken ct = default);
    Task UpdateAsync(Embryo embryo, CancellationToken ct = default);
    Task DeleteAsync(Embryo embryo, CancellationToken ct = default);
    Task<int> GetNextNumberForCycleAsync(Guid cycleId, CancellationToken ct = default);
    Task<IReadOnlyList<Embryo>> GetActiveAsync(CancellationToken ct = default);
}
