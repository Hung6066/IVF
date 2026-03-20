using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IEggDonorRecipientRepository
{
    Task<EggDonorRecipient?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<EggDonorRecipient>> GetByDonorAsync(Guid eggDonorId, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<EggDonorRecipient>> GetByRecipientCoupleAsync(Guid coupleId, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<EggDonorRecipient>> GetByCycleAsync(Guid cycleId, Guid tenantId, CancellationToken ct = default);
    Task<bool> MatchExistsAsync(Guid eggDonorId, Guid coupleId, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(EggDonorRecipient match, CancellationToken ct = default);
    Task UpdateAsync(EggDonorRecipient match, CancellationToken ct = default);
}
