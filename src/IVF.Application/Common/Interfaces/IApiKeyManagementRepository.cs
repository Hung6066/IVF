using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IApiKeyManagementRepository
{
    Task<ApiKeyManagement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiKeyManagement?> GetByKeyNameAsync(string serviceName, string keyName, CancellationToken ct = default);
    Task<List<ApiKeyManagement>> GetByServiceAsync(string serviceName, CancellationToken ct = default);
    Task<List<ApiKeyManagement>> GetActiveKeysAsync(string serviceName, CancellationToken ct = default);
    Task<List<ApiKeyManagement>> GetExpiringKeysAsync(int withinDays, CancellationToken ct = default);
    Task AddAsync(ApiKeyManagement key, CancellationToken ct = default);
    Task UpdateAsync(ApiKeyManagement key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string serviceName, string keyName, CancellationToken ct = default);
}
