using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface ISemenAnalysisRepository
{
    Task<SemenAnalysis?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SemenAnalysis>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<SemenAnalysis>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<SemenAnalysis> AddAsync(SemenAnalysis analysis, CancellationToken ct = default);
    Task UpdateAsync(SemenAnalysis analysis, CancellationToken ct = default);
}

public interface ISpermDonorRepository
{
    Task<SpermDonor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SpermDonor?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<(IReadOnlyList<SpermDonor> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default);
    Task<SpermDonor> AddAsync(SpermDonor donor, CancellationToken ct = default);
    Task UpdateAsync(SpermDonor donor, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);
}

public interface ISpermSampleRepository
{
    Task<SpermSample?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SpermSample>> GetByDonorIdAsync(Guid donorId, CancellationToken ct = default);
    Task<IReadOnlyList<SpermSample>> GetAvailableAsync(CancellationToken ct = default);
    Task<SpermSample> AddAsync(SpermSample sample, CancellationToken ct = default);
    Task UpdateAsync(SpermSample sample, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);
}

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
}

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default);
    Task<Payment> AddAsync(Payment payment, CancellationToken ct = default);
    Task<string> GeneratePaymentNumberAsync(CancellationToken ct = default);
}
