using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class MedicationAdministrationRepository : IMedicationAdministrationRepository
{
    private readonly IvfDbContext _context;
    public MedicationAdministrationRepository(IvfDbContext context) => _context = context;

    public async Task<MedicationAdministration?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.MedicationAdministrations
            .Include(m => m.Patient)
            .Include(m => m.Cycle)
            .Include(m => m.AdministeredBy)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyList<MedicationAdministration>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.MedicationAdministrations
            .AsNoTracking()
            .Include(m => m.Patient)
            .Include(m => m.AdministeredBy)
            .Where(m => m.CycleId == cycleId)
            .OrderByDescending(m => m.AdministeredAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MedicationAdministration>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _context.MedicationAdministrations
            .AsNoTracking()
            .Include(m => m.AdministeredBy)
            .Where(m => m.PatientId == patientId)
            .OrderByDescending(m => m.AdministeredAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MedicationAdministration>> GetTriggerShotsByCycleAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.MedicationAdministrations
            .AsNoTracking()
            .Include(m => m.AdministeredBy)
            .Where(m => m.CycleId == cycleId && m.IsTriggerShot)
            .OrderByDescending(m => m.AdministeredAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<MedicationAdministration> Items, int Total)> SearchAsync(string? query, Guid? cycleId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.MedicationAdministrations.Include(m => m.Patient).Include(m => m.AdministeredBy).AsQueryable();

        if (!string.IsNullOrEmpty(query))
            q = q.Where(m => m.MedicationName.Contains(query) || m.Patient.FullName.Contains(query));
        if (cycleId.HasValue)
            q = q.Where(m => m.CycleId == cycleId.Value);
        if (fromDate.HasValue)
            q = q.Where(m => m.AdministeredAt >= fromDate.Value);
        if (toDate.HasValue)
            q = q.Where(m => m.AdministeredAt <= toDate.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(m => m.AdministeredAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<MedicationAdministration> AddAsync(MedicationAdministration med, CancellationToken ct = default)
    {
        await _context.MedicationAdministrations.AddAsync(med, ct);
        return med;
    }

    public async Task UpdateAsync(MedicationAdministration med, CancellationToken ct = default)
    {
        _context.MedicationAdministrations.Update(med);
        await Task.CompletedTask;
    }
}
