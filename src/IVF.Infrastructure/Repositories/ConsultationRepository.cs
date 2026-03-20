using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class ConsultationRepository : IConsultationRepository
{
    private readonly IvfDbContext _context;
    public ConsultationRepository(IvfDbContext context) => _context = context;

    public async Task<Consultation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Consultations
            .Include(c => c.Patient)
            .Include(c => c.Doctor)
            .Include(c => c.Cycle)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Consultation>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _context.Consultations
            .AsNoTracking()
            .Include(c => c.Doctor)
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.ConsultationDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Consultation>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.Consultations
            .AsNoTracking()
            .Include(c => c.Patient)
            .Include(c => c.Doctor)
            .Where(c => c.CycleId == cycleId)
            .OrderByDescending(c => c.ConsultationDate)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Consultation> Items, int Total)> SearchAsync(string? query, string? status, string? type, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.Consultations
            .Include(c => c.Patient)
            .Include(c => c.Doctor)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query))
            q = q.Where(c => c.Patient.FullName.Contains(query) || c.Patient.PatientCode.Contains(query));

        if (!string.IsNullOrEmpty(status))
            q = q.Where(c => c.Status == status);

        if (!string.IsNullOrEmpty(type))
            q = q.Where(c => c.ConsultationType == type);

        if (fromDate.HasValue)
            q = q.Where(c => c.ConsultationDate >= fromDate.Value);

        if (toDate.HasValue)
            q = q.Where(c => c.ConsultationDate <= toDate.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(c => c.ConsultationDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Consultation> AddAsync(Consultation consultation, CancellationToken ct = default)
    {
        await _context.Consultations.AddAsync(consultation, ct);
        return consultation;
    }

    public async Task UpdateAsync(Consultation consultation, CancellationToken ct = default)
    {
        _context.Consultations.Update(consultation);
        await Task.CompletedTask;
    }
}
