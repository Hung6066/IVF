using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class LabOrderRepository : ILabOrderRepository
{
    private readonly IvfDbContext _context;
    public LabOrderRepository(IvfDbContext context) => _context = context;

    public async Task<LabOrder?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.LabOrders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<LabOrder?> GetByIdWithTestsAsync(Guid id, CancellationToken ct = default)
        => await _context.LabOrders
            .Include(o => o.Patient)
            .Include(o => o.OrderedBy)
            .Include(o => o.Cycle)
            .Include(o => o.Tests)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<LabOrder>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _context.LabOrders
            .AsNoTracking()
            .Include(o => o.OrderedBy)
            .Include(o => o.Tests)
            .Where(o => o.PatientId == patientId)
            .OrderByDescending(o => o.OrderedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LabOrder>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.LabOrders
            .AsNoTracking()
            .Include(o => o.Patient)
            .Include(o => o.OrderedBy)
            .Include(o => o.Tests)
            .Where(o => o.CycleId == cycleId)
            .OrderByDescending(o => o.OrderedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<LabOrder> Items, int Total)> SearchAsync(string? query, string? status, string? orderType, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.LabOrders
            .Include(o => o.Patient)
            .Include(o => o.OrderedBy)
            .Include(o => o.Tests)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query))
            q = q.Where(o => o.Patient.FullName.Contains(query) || o.Patient.PatientCode.Contains(query));

        if (!string.IsNullOrEmpty(status))
            q = q.Where(o => o.Status == status);

        if (!string.IsNullOrEmpty(orderType))
            q = q.Where(o => o.OrderType == orderType);

        if (fromDate.HasValue)
            q = q.Where(o => o.OrderedAt >= fromDate.Value);

        if (toDate.HasValue)
            q = q.Where(o => o.OrderedAt <= toDate.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(o => o.OrderedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<int> GetCountByStatusAsync(string status, CancellationToken ct = default)
        => await _context.LabOrders.CountAsync(o => o.Status == status, ct);

    public async Task<LabOrder> AddAsync(LabOrder order, CancellationToken ct = default)
    {
        await _context.LabOrders.AddAsync(order, ct);
        return order;
    }

    public async Task UpdateAsync(LabOrder order, CancellationToken ct = default)
    {
        _context.LabOrders.Update(order);
        await Task.CompletedTask;
    }
}
