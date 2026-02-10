using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly IvfDbContext _context;
    public InvoiceRepository(IvfDbContext context) => _context = context;

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<Invoice?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default)
        => await _context.Invoices.AsNoTracking().Include(i => i.Items).Include(i => i.Patient).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<Invoice>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _context.Invoices.AsNoTracking().Include(i => i.Patient).Where(i => i.PatientId == patientId).OrderByDescending(i => i.InvoiceDate).ToListAsync(ct);

    public async Task<(IReadOnlyList<Invoice> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.Invoices.AsNoTracking().Include(i => i.Patient).AsQueryable();
        if (!string.IsNullOrEmpty(query))
            q = q.Where(i => i.InvoiceNumber.Contains(query) || i.Patient.FullName.Contains(query));
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(i => i.InvoiceDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<Invoice> AddAsync(Invoice invoice, CancellationToken ct = default)
    { await _context.Invoices.AddAsync(invoice, ct); return invoice; }

    public Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
    { _context.Invoices.Update(invoice); return Task.CompletedTask; }

    public async Task<string> GenerateInvoiceNumberAsync(CancellationToken ct = default)
    {
        var count = await _context.Invoices.CountAsync(ct);
        return $"INV-{DateTime.Now:yyyy}-{count + 1:D6}";
    }

    public async Task<decimal> GetMonthlyRevenueAsync(int month, int year, CancellationToken ct = default)
    {
        // Use date range instead of .Month/.Year extraction to enable index usage
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        return await _context.Invoices
            .Where(i => i.InvoiceDate >= start && i.InvoiceDate < end && i.Status != Domain.Enums.InvoiceStatus.Cancelled)
            .SumAsync(i => i.PaidAmount, ct);
    }

    public async Task<Dictionary<int, decimal>> GetYearlyRevenueByMonthAsync(int year, CancellationToken ct = default)
    {
        var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var revenue = await _context.Invoices
            .Where(i => i.InvoiceDate >= start && i.InvoiceDate < end && i.Status != Domain.Enums.InvoiceStatus.Cancelled)
            .GroupBy(i => i.InvoiceDate.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(i => i.PaidAmount) })
            .ToDictionaryAsync(x => x.Month, x => x.Total, ct);

        return revenue;
    }
}
