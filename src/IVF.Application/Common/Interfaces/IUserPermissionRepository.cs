using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IUserPermissionRepository
{
    Task<IReadOnlyList<UserPermission>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct = default);
    Task AddAsync(UserPermission permission, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<UserPermission> permissions, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, string permissionCode, CancellationToken ct = default);
    Task DeleteAllByUserIdAsync(Guid userId, CancellationToken ct = default);
}
