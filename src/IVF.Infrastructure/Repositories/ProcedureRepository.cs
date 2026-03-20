using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class ProcedureRepository(IvfDbContext context) : IProcedureRepository
{
    public async Task<Procedure?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Procedures
            .Include(p => p.Patient)
            .Include(p => p.PerformedByDoctor).ThenInclude(d => d.User)
            .Include(p => p.AssistantDoctor).ThenInclude(d => d!.User)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Procedure>> GetByPatientAsync(Guid patientId, CancellationToken ct = default)
        => await context.Procedures
            .Include(p => p.Patient)
            .Include(p => p.PerformedByDoctor).ThenInclude(d => d.User)
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.ScheduledAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Procedure>> GetByCycleAsync(Guid cycleId, CancellationToken ct = default)
        => await context.Procedures
            .Include(p => p.Patient)
            .Include(p => p.PerformedByDoctor).ThenInclude(d => d.User)
            .Where(p => p.CycleId == cycleId)
            .OrderByDescending(p => p.ScheduledAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Procedure>> GetByDateAsync(DateTime date, CancellationToken ct = default)
        => await context.Procedures
            .Include(p => p.Patient)
            .Include(p => p.PerformedByDoctor).ThenInclude(d => d.User)
            .Where(p => p.ScheduledAt.Date == date.Date)
            .OrderBy(p => p.ScheduledAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Procedure>> SearchAsync(string? query, string? procedureType, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = context.Procedures
            .Include(p => p.Patient)
            .Include(p => p.PerformedByDoctor).ThenInclude(d => d.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Patient.FullName.Contains(query) || p.Patient.PatientCode.Contains(query) || p.ProcedureName.Contains(query));
        if (!string.IsNullOrWhiteSpace(procedureType))
            q = q.Where(p => p.ProcedureType == procedureType);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(p => p.Status == status);

        return await q.OrderByDescending(p => p.ScheduledAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    }

    public async Task<int> CountAsync(string? query, string? procedureType, string? status, CancellationToken ct = default)
    {
        var q = context.Procedures.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Patient.FullName.Contains(query) || p.Patient.PatientCode.Contains(query));
        if (!string.IsNullOrWhiteSpace(procedureType))
            q = q.Where(p => p.ProcedureType == procedureType);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(p => p.Status == status);
        return await q.CountAsync(ct);
    }

    public async Task<Procedure> AddAsync(Procedure procedure, CancellationToken ct = default)
    {
        await context.Procedures.AddAsync(procedure, ct);
        return procedure;
    }

    public Task UpdateAsync(Procedure procedure, CancellationToken ct = default)
    {
        context.Procedures.Update(procedure);
        return Task.CompletedTask;
    }
}
