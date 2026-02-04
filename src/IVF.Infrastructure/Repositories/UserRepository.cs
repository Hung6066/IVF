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
}
