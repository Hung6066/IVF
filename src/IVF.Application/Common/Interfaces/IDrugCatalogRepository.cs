using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IDrugCatalogRepository
{
    Task<DrugCatalog?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<DrugCatalog?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<DrugCatalog>> GetByCategoryAsync(DrugCategory category, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<DrugCatalog>> GetActiveAsync(Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<DrugCatalog> Items, int Total)> SearchAsync(string? query, DrugCategory? category, bool? isActive, int page, int pageSize, Guid tenantId, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(DrugCatalog drug, CancellationToken ct = default);
    Task UpdateAsync(DrugCatalog drug, CancellationToken ct = default);
}
