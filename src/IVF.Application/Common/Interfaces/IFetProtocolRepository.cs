using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IFetProtocolRepository
{
    Task<FetProtocol?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<FetProtocol?> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<IReadOnlyList<FetProtocol>> SearchAsync(string? query = null, string? status = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<int> CountAsync(string? query = null, string? status = null, CancellationToken ct = default);
    Task<FetProtocol> AddAsync(FetProtocol protocol, CancellationToken ct = default);
    Task UpdateAsync(FetProtocol protocol, CancellationToken ct = default);
}
