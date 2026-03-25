using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly IvfDbContext _context;
    public PaymentRepository(IvfDbContext context) => _context = context;

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
        => await _context.Payments.Where(p => p.InvoiceId == invoiceId).OrderByDescending(p => p.PaymentDate).ToListAsync(ct);

    public async Task<Payment> AddAsync(Payment payment, CancellationToken ct = default)
    { await _context.Payments.AddAsync(payment, ct); return payment; }

    public async Task<string> GeneratePaymentNumberAsync(CancellationToken ct = default)
    {
        var prefix = $"PAY-{DateTime.Now:yyyyMMdd}-";
        return await CodeGenerator.NextAsync(_context,
            () => _context.Payments.IgnoreQueryFilters()
                .Where(p => p.PaymentNumber.StartsWith(prefix))
                .OrderByDescending(p => p.PaymentNumber)
                .Select(p => p.PaymentNumber)
                .FirstOrDefaultAsync(ct),
            prefix, padding: 6, ct);
    }
}
