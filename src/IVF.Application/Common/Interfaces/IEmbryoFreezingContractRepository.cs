using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IEmbryoFreezingContractRepository
{
    Task<EmbryoFreezingContract?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EmbryoFreezingContract>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<IReadOnlyList<EmbryoFreezingContract>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<EmbryoFreezingContract>> GetExpiringAsync(DateTime before, CancellationToken ct = default);
    Task<EmbryoFreezingContract> AddAsync(EmbryoFreezingContract contract, CancellationToken ct = default);
    Task UpdateAsync(EmbryoFreezingContract contract, CancellationToken ct = default);
    Task<string> GenerateContractNumberAsync(CancellationToken ct = default);
}
