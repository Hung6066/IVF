using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IEggDonorRepository
{
    Task<EggDonor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<EggDonor?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<(IReadOnlyList<EggDonor> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default);
    Task<EggDonor> AddAsync(EggDonor donor, CancellationToken ct = default);
    Task UpdateAsync(EggDonor donor, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);
}

public interface IOocyteSampleRepository
{
    Task<OocyteSample?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OocyteSample>> GetByDonorIdAsync(Guid donorId, CancellationToken ct = default);
    Task<IReadOnlyList<OocyteSample>> GetAvailableAsync(CancellationToken ct = default);
    Task<OocyteSample> AddAsync(OocyteSample sample, CancellationToken ct = default);
    Task UpdateAsync(OocyteSample sample, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);
}
