using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IInventoryRequestRepository
{
    Task<InventoryRequest?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryRequest>> GetByStatusAsync(InventoryRequestStatus status, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryRequest>> GetByRequestedUserAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<InventoryRequest> Items, int Total)> SearchAsync(string? query, InventoryRequestStatus? status, InventoryRequestType? type, int page, int pageSize, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(InventoryRequest request, CancellationToken ct = default);
    Task UpdateAsync(InventoryRequest request, CancellationToken ct = default);
}
