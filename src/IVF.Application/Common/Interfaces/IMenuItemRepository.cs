using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IMenuItemRepository
{
    Task<IReadOnlyList<MenuItem>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MenuItem>> GetActiveAsync(CancellationToken ct = default);
    Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MenuItem?> GetByRouteAsync(string route, CancellationToken ct = default);
    Task AddAsync(MenuItem item, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<MenuItem> items, CancellationToken ct = default);
    Task UpdateAsync(MenuItem item, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
}
