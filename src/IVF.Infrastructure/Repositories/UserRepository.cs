using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IvfDbContext _context;

    public UserRepository(IvfDbContext context) => _context = context;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.IsActive, ct);

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }

    public async Task<List<User>> GetUsersByRoleAsync(string role, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Users.Where(u => u.Role == role && u.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.FullName.Contains(search) || u.Username.Contains(search));
        }

        return await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await _context.Users.AddAsync(user, ct);
    }

    public Task DeleteAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Remove(user);
        return Task.CompletedTask;
    }

    public async Task<List<User>> SearchUsersAsync(string? search, string? role, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(search, role, isActive);
        return await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountUsersAsync(string? search, string? role, bool? isActive, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(search, role, isActive);
        return await query.CountAsync(ct);
    }

    private IQueryable<User> BuildSearchQuery(string? search, string? role, bool? isActive)
    {
        var query = _context.Users.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role == role);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.FullName.Contains(search) || u.Username.Contains(search) || (u.Department != null && u.Department.Contains(search)));

        return query;
    }
}
