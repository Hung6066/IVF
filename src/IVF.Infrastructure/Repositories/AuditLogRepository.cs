using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly IvfDbContext _context;
    
    public AuditLogRepository(IvfDbContext context) => _context = context;

    public async Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default)
        => await _context.AuditLogs
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AuditLog>> GetByUserAsync(Guid userId, int take = 100, CancellationToken ct = default)
        => await _context.AuditLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take = 100, CancellationToken ct = default)
        => await _context.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AuditLog>> SearchAsync(
        string? entityType = null,
        string? action = null,
        Guid? userId = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(a => a.EntityType == entityType);
        
        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);
        
        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId);
        
        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from);
        
        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }
}
