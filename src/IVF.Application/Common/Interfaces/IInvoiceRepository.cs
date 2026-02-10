using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Invoice?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Invoice>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<(IReadOnlyList<Invoice> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default);
    Task<Invoice> AddAsync(Invoice invoice, CancellationToken ct = default);
    Task UpdateAsync(Invoice invoice, CancellationToken ct = default);
    Task<string> GenerateInvoiceNumberAsync(CancellationToken ct = default);
    // Reporting
    Task<decimal> GetMonthlyRevenueAsync(int month, int year, CancellationToken ct = default);
    Task<Dictionary<int, decimal>> GetYearlyRevenueByMonthAsync(int year, CancellationToken ct = default);
}
