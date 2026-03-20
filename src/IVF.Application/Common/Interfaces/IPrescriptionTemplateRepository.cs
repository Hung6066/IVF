using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IPrescriptionTemplateRepository
{
    Task<PrescriptionTemplate?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<PrescriptionTemplate>> GetByDoctorAsync(Guid doctorId, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<PrescriptionTemplate>> GetByCycleTypeAsync(PrescriptionCycleType cycleType, Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<PrescriptionTemplate> Items, int Total)> SearchAsync(string? query, PrescriptionCycleType? cycleType, bool? isActive, int page, int pageSize, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(PrescriptionTemplate template, CancellationToken ct = default);
    Task UpdateAsync(PrescriptionTemplate template, CancellationToken ct = default);
}
