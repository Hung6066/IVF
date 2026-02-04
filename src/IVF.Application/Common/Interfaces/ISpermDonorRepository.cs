using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface ISpermDonorRepository
{
    Task<SpermDonor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SpermDonor?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<(IReadOnlyList<SpermDonor> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default);
    Task<SpermDonor> AddAsync(SpermDonor donor, CancellationToken ct = default);
    Task UpdateAsync(SpermDonor donor, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);
}
