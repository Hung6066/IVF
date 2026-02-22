using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IPermissionDefinitionRepository
{
    Task<IReadOnlyList<PermissionDefinition>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PermissionDefinition>> GetActiveAsync(CancellationToken ct = default);
    Task<PermissionDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PermissionDefinition?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task AddAsync(PermissionDefinition entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<PermissionDefinition> entities, CancellationToken ct = default);
    Task UpdateAsync(PermissionDefinition entity, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
}
