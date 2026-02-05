using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IUserPermissionRepository
{
    Task<IReadOnlyList<UserPermission>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> HasPermissionAsync(Guid userId, Permission permission, CancellationToken ct = default);
    Task AddAsync(UserPermission permission, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<UserPermission> permissions, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Permission permission, CancellationToken ct = default);
    Task DeleteAllByUserIdAsync(Guid userId, CancellationToken ct = default);
}
