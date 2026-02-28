using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class ApiKeyManagementRepository : IApiKeyManagementRepository
{
    private readonly IvfDbContext _context;

    public ApiKeyManagementRepository(IvfDbContext context) => _context = context;

    public async Task<ApiKeyManagement?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.ApiKeyManagements.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task<ApiKeyManagement?> GetByKeyNameAsync(string serviceName, string keyName, CancellationToken ct = default)
        => await _context.ApiKeyManagements.AsNoTracking()
            .FirstOrDefaultAsync(k => k.ServiceName == serviceName && k.KeyName == keyName, ct);

    public async Task<List<ApiKeyManagement>> GetByServiceAsync(string serviceName, CancellationToken ct = default)
        => await _context.ApiKeyManagements.AsNoTracking()
            .Where(k => k.ServiceName == serviceName)
            .OrderBy(k => k.KeyName)
            .ToListAsync(ct);

    public async Task<List<ApiKeyManagement>> GetActiveKeysAsync(string serviceName, CancellationToken ct = default)
        => await _context.ApiKeyManagements.AsNoTracking()
            .Where(k => k.ServiceName == serviceName && k.IsActive)
            .OrderBy(k => k.KeyName)
            .ToListAsync(ct);

    public async Task<List<ApiKeyManagement>> GetExpiringKeysAsync(int withinDays, CancellationToken ct = default)
    {
        var threshold = DateTime.UtcNow.AddDays(withinDays);
        return await _context.ApiKeyManagements.AsNoTracking()
            .Where(k => k.IsActive && k.ExpiresAt != null && k.ExpiresAt <= threshold)
            .OrderBy(k => k.ExpiresAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ApiKeyManagement key, CancellationToken ct = default)
        => await _context.ApiKeyManagements.AddAsync(key, ct);

    public Task UpdateAsync(ApiKeyManagement key, CancellationToken ct = default)
    {
        _context.ApiKeyManagements.Update(key);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string serviceName, string keyName, CancellationToken ct = default)
        => await _context.ApiKeyManagements.AnyAsync(
            k => k.ServiceName == serviceName && k.KeyName == keyName, ct);
}
