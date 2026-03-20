using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class InventoryRequestRepository : IInventoryRequestRepository
{
    private readonly IvfDbContext _context;
    public InventoryRequestRepository(IvfDbContext context) => _context = context;

    public async Task<InventoryRequest?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _context.InventoryRequests
            .Include(r => r.RequestedBy)
            .Include(r => r.ApprovedBy)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<InventoryRequest>> GetByStatusAsync(InventoryRequestStatus status, Guid tenantId, CancellationToken ct = default)
        => await _context.InventoryRequests
            .AsNoTracking()
            .Include(r => r.RequestedBy)
            .Where(r => r.Status == status && r.TenantId == tenantId)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<InventoryRequest>> GetByRequestedUserAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
        => await _context.InventoryRequests
            .AsNoTracking()
            .Where(r => r.RequestedByUserId == userId && r.TenantId == tenantId)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<InventoryRequest> Items, int Total)> SearchAsync(string? query, InventoryRequestStatus? status, InventoryRequestType? type, int page, int pageSize, Guid tenantId, CancellationToken ct = default)
    {
        var q = _context.InventoryRequests.AsNoTracking()
            .Include(r => r.RequestedBy)
            .Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrEmpty(query))
            q = q.Where(r => r.ItemName.Contains(query));

        if (status.HasValue)
            q = q.Where(r => r.Status == status.Value);

        if (type.HasValue)
            q = q.Where(r => r.RequestType == type.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.RequestedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task AddAsync(InventoryRequest request, CancellationToken ct = default)
        => await _context.InventoryRequests.AddAsync(request, ct);

    public Task UpdateAsync(InventoryRequest request, CancellationToken ct = default)
    {
        _context.InventoryRequests.Update(request);
        return Task.CompletedTask;
    }
}
