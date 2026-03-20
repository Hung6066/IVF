using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface ICycleFeeRepository
{
    Task<CycleFee?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CycleFee>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<bool> HasFeeForTypeAsync(Guid cycleId, string feeType, CancellationToken ct = default);
    Task<CycleFee> AddAsync(CycleFee fee, CancellationToken ct = default);
    Task UpdateAsync(CycleFee fee, CancellationToken ct = default);
}
