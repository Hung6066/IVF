using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class PrescriptionTemplateRepository : IPrescriptionTemplateRepository
{
    private readonly IvfDbContext _context;
    public PrescriptionTemplateRepository(IvfDbContext context) => _context = context;

    public async Task<PrescriptionTemplate?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _context.PrescriptionTemplates
            .Include(t => t.Items)
            .Include(t => t.CreatedByDoctor)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<PrescriptionTemplate>> GetByDoctorAsync(Guid doctorId, Guid tenantId, CancellationToken ct = default)
        => await _context.PrescriptionTemplates
            .AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.CreatedByDoctorId == doctorId && t.TenantId == tenantId)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PrescriptionTemplate>> GetByCycleTypeAsync(PrescriptionCycleType cycleType, Guid tenantId, CancellationToken ct = default)
        => await _context.PrescriptionTemplates
            .AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.CycleType == cycleType && t.TenantId == tenantId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<PrescriptionTemplate> Items, int Total)> SearchAsync(string? query, PrescriptionCycleType? cycleType, bool? isActive, int page, int pageSize, Guid tenantId, CancellationToken ct = default)
    {
        var q = _context.PrescriptionTemplates.AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.TenantId == tenantId);

        if (!string.IsNullOrEmpty(query))
            q = q.Where(t => t.Name.Contains(query));

        if (cycleType.HasValue)
            q = q.Where(t => t.CycleType == cycleType.Value);

        if (isActive.HasValue)
            q = q.Where(t => t.IsActive == isActive.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(t => t.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task AddAsync(PrescriptionTemplate template, CancellationToken ct = default)
        => await _context.PrescriptionTemplates.AddAsync(template, ct);

    public Task UpdateAsync(PrescriptionTemplate template, CancellationToken ct = default)
    {
        _context.PrescriptionTemplates.Update(template);
        return Task.CompletedTask;
    }
}
