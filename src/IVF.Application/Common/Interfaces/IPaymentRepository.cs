using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default);
    Task<Payment> AddAsync(Payment payment, CancellationToken ct = default);
    Task<string> GeneratePaymentNumberAsync(CancellationToken ct = default);
}
