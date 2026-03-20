using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class ConsentFormRepository : IConsentFormRepository
{
    private readonly IvfDbContext _context;
    public ConsentFormRepository(IvfDbContext context) => _context = context;

    public async Task<ConsentForm?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.ConsentForms
            .Include(c => c.Patient)
            .Include(c => c.Cycle)
            .Include(c => c.Procedure)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ConsentForm>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _context.ConsentForms
            .AsNoTracking()
            .Include(c => c.Patient)
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ConsentForm>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.ConsentForms
            .AsNoTracking()
            .Include(c => c.Patient)
            .Where(c => c.CycleId == cycleId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ConsentForm>> GetPendingByPatientAsync(Guid patientId, CancellationToken ct = default)
        => await _context.ConsentForms
            .AsNoTracking()
            .Include(c => c.Patient)
            .Where(c => c.PatientId == patientId && c.Status == "Pending")
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<ConsentForm> Items, int Total)> SearchAsync(string? query, string? status, string? consentType, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.ConsentForms.Include(c => c.Patient).AsQueryable();

        if (!string.IsNullOrEmpty(query))
            q = q.Where(c => c.Patient.FullName.Contains(query) || c.Title.Contains(query));
        if (!string.IsNullOrEmpty(status))
            q = q.Where(c => c.Status == status);
        if (!string.IsNullOrEmpty(consentType))
            q = q.Where(c => c.ConsentType == consentType);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<bool> HasValidConsentAsync(Guid patientId, string consentType, Guid? cycleId, CancellationToken ct = default)
    {
        var q = _context.ConsentForms.Where(c => c.PatientId == patientId && c.ConsentType == consentType && c.Status == "Signed");
        if (cycleId.HasValue)
            q = q.Where(c => c.CycleId == cycleId.Value);
        var consent = await q.FirstOrDefaultAsync(ct);
        return consent != null && consent.IsValid;
    }

    public async Task<ConsentForm> AddAsync(ConsentForm consent, CancellationToken ct = default)
    {
        await _context.ConsentForms.AddAsync(consent, ct);
        return consent;
    }

    public async Task UpdateAsync(ConsentForm consent, CancellationToken ct = default)
    {
        _context.ConsentForms.Update(consent);
        await Task.CompletedTask;
    }
}
