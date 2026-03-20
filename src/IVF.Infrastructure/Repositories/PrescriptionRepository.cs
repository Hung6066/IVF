using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class PrescriptionRepository : IPrescriptionRepository
{
    private readonly IvfDbContext _context;
    public PrescriptionRepository(IvfDbContext context) => _context = context;

    public async Task<Prescription?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Prescriptions.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Prescription?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default)
        => await _context.Prescriptions
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Include(p => p.Cycle)
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Prescription>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _context.Prescriptions
            .AsNoTracking()
            .Include(p => p.Doctor)
            .Include(p => p.Items)
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.PrescriptionDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Prescription>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.Prescriptions
            .AsNoTracking()
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Include(p => p.Items)
            .Where(p => p.CycleId == cycleId)
            .OrderByDescending(p => p.PrescriptionDate)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Prescription> Items, int Total)> SearchAsync(string? query, DateTime? fromDate, DateTime? toDate, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.Prescriptions
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Include(p => p.Items)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query))
            q = q.Where(p => p.Patient.FullName.Contains(query) || p.Patient.PatientCode.Contains(query));

        if (fromDate.HasValue)
            q = q.Where(p => p.PrescriptionDate >= fromDate.Value);

        if (toDate.HasValue)
            q = q.Where(p => p.PrescriptionDate <= toDate.Value);

        if (!string.IsNullOrEmpty(status))
            q = q.Where(p => p.Status == status);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.PrescriptionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<int> GetCountByDateAsync(DateTime date, CancellationToken ct = default)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        return await _context.Prescriptions.CountAsync(p => p.PrescriptionDate >= start && p.PrescriptionDate < end, ct);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
        => await _context.Prescriptions.CountAsync(p => p.Status == "Pending", ct);

    public async Task<Prescription> AddAsync(Prescription prescription, CancellationToken ct = default)
    {
        await _context.Prescriptions.AddAsync(prescription, ct);
        return prescription;
    }

    public async Task UpdateAsync(Prescription prescription, CancellationToken ct = default)
    {
        _context.Prescriptions.Update(prescription);
        await Task.CompletedTask;
    }
}
