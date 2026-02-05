using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IServiceCatalogRepository
{
    Task<ServiceCatalog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceCatalog?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceCatalog>> GetAllAsync(ServiceCategory? category = null, bool? isActive = null, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceCatalog>> SearchAsync(string? query, ServiceCategory? category = null, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<int> CountAsync(string? query, ServiceCategory? category = null, CancellationToken ct = default);
    Task AddAsync(ServiceCatalog service, CancellationToken ct = default);
    Task UpdateAsync(ServiceCatalog service, CancellationToken ct = default);
}
