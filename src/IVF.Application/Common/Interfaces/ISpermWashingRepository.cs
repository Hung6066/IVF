using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface ISpermWashingRepository
{
    Task<SpermWashing?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SpermWashing>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<(IReadOnlyList<SpermWashing> Items, int Total)> SearchAsync(string? method, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken ct = default);
    Task<SpermWashing> AddAsync(SpermWashing washing, CancellationToken ct = default);
    Task UpdateAsync(SpermWashing washing, CancellationToken ct = default);
    
    // Stats
    Task<int> GetCountByDateAsync(DateTime date, CancellationToken ct = default);
}
