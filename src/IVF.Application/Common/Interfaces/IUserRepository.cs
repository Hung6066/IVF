using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task<List<User>> GetUsersByRoleAsync(string role, string? search, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task DeleteAsync(User user, CancellationToken ct = default);
    Task<List<User>> SearchUsersAsync(string? search, string? role, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountUsersAsync(string? search, string? role, bool? isActive, CancellationToken ct = default);
}
