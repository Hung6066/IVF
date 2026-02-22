using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class UserPermissionRepository : IUserPermissionRepository
{
    private readonly IvfDbContext _context;

    public UserPermissionRepository(IvfDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<UserPermission>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.UserPermissions
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct = default)
    {
        return await _context.UserPermissions
            .AnyAsync(p => p.UserId == userId && p.PermissionCode == permissionCode, ct);
    }

    public async Task AddAsync(UserPermission permission, CancellationToken ct = default)
    {
        await _context.UserPermissions.AddAsync(permission, ct);
    }

    public async Task AddRangeAsync(IEnumerable<UserPermission> permissions, CancellationToken ct = default)
    {
        await _context.UserPermissions.AddRangeAsync(permissions, ct);
    }

    public async Task DeleteAsync(Guid userId, string permissionCode, CancellationToken ct = default)
    {
        var entity = await _context.UserPermissions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PermissionCode == permissionCode, ct);

        if (entity != null)
        {
            _context.UserPermissions.Remove(entity);
        }
    }

    public async Task DeleteAllByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var permissions = await _context.UserPermissions
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        _context.UserPermissions.RemoveRange(permissions);
    }
}
