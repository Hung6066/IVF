using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class SemenAnalysisRepository : ISemenAnalysisRepository
{
    private readonly IvfDbContext _context;
    public SemenAnalysisRepository(IvfDbContext context) => _context = context;

    public async Task<SemenAnalysis?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.SemenAnalyses.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SemenAnalysis>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _context.SemenAnalyses.Where(s => s.PatientId == patientId).OrderByDescending(s => s.AnalysisDate).ToListAsync(ct);

    public async Task<IReadOnlyList<SemenAnalysis>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.SemenAnalyses.Where(s => s.CycleId == cycleId).OrderByDescending(s => s.AnalysisDate).ToListAsync(ct);

    public async Task<SemenAnalysis> AddAsync(SemenAnalysis analysis, CancellationToken ct = default)
    { await _context.SemenAnalyses.AddAsync(analysis, ct); return analysis; }

    public Task UpdateAsync(SemenAnalysis analysis, CancellationToken ct = default)
    { _context.SemenAnalyses.Update(analysis); return Task.CompletedTask; }
}

public class SpermDonorRepository : ISpermDonorRepository
{
    private readonly IvfDbContext _context;
    public SpermDonorRepository(IvfDbContext context) => _context = context;

    public async Task<SpermDonor?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.SpermDonors.Include(d => d.Patient).FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<SpermDonor?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _context.SpermDonors.Include(d => d.Patient).FirstOrDefaultAsync(d => d.DonorCode == code, ct);

    public async Task<(IReadOnlyList<SpermDonor> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.SpermDonors.Include(d => d.Patient).AsQueryable();
        if (!string.IsNullOrEmpty(query))
            q = q.Where(d => d.DonorCode.Contains(query) || d.Patient.FullName.Contains(query));
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(d => d.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<SpermDonor> AddAsync(SpermDonor donor, CancellationToken ct = default)
    { await _context.SpermDonors.AddAsync(donor, ct); return donor; }

    public Task UpdateAsync(SpermDonor donor, CancellationToken ct = default)
    { _context.SpermDonors.Update(donor); return Task.CompletedTask; }

    public async Task<string> GenerateCodeAsync(CancellationToken ct = default)
    {
        var count = await _context.SpermDonors.CountAsync(ct);
        return $"NHTT-{DateTime.Now:yyyy}-{count + 1:D4}";
    }
}

public class SpermSampleRepository : ISpermSampleRepository
{
    private readonly IvfDbContext _context;
    public SpermSampleRepository(IvfDbContext context) => _context = context;

    public async Task<SpermSample?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.SpermSamples.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SpermSample>> GetByDonorIdAsync(Guid donorId, CancellationToken ct = default)
        => await _context.SpermSamples.Where(s => s.DonorId == donorId).OrderByDescending(s => s.CollectionDate).ToListAsync(ct);

    public async Task<IReadOnlyList<SpermSample>> GetAvailableAsync(CancellationToken ct = default)
        => await _context.SpermSamples.Include(s => s.Donor).Where(s => s.IsAvailable).ToListAsync(ct);

    public async Task<SpermSample> AddAsync(SpermSample sample, CancellationToken ct = default)
    { await _context.SpermSamples.AddAsync(sample, ct); return sample; }

    public Task UpdateAsync(SpermSample sample, CancellationToken ct = default)
    { _context.SpermSamples.Update(sample); return Task.CompletedTask; }

    public async Task<string> GenerateCodeAsync(CancellationToken ct = default)
    {
        var count = await _context.SpermSamples.CountAsync(ct);
        return $"SP-{DateTime.Now:yyyyMMdd}-{count + 1:D4}";
    }
}

public class InvoiceRepository : IInvoiceRepository
{
    private readonly IvfDbContext _context;
    public InvoiceRepository(IvfDbContext context) => _context = context;

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<Invoice?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default)
        => await _context.Invoices.Include(i => i.Items).Include(i => i.Patient).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<Invoice>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _context.Invoices.Include(i => i.Patient).Where(i => i.PatientId == patientId).OrderByDescending(i => i.InvoiceDate).ToListAsync(ct);

    public async Task<(IReadOnlyList<Invoice> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.Invoices.Include(i => i.Patient).AsQueryable();
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
        return await _context.Invoices
            .Where(i => i.InvoiceDate.Month == month && i.InvoiceDate.Year == year && i.Status != Domain.Enums.InvoiceStatus.Cancelled)
            .SumAsync(i => i.PaidAmount, ct);
    }
}

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
        var count = await _context.Payments.CountAsync(ct);
        return $"PAY-{DateTime.Now:yyyyMMdd}-{count + 1:D6}";
    }
}
