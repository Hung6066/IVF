using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByUserAsync(Guid userId, int take = 100, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take = 100, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> SearchAsync(
        string? entityType = null,
        string? action = null,
        Guid? userId = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);
}
