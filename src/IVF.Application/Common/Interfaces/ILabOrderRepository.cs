using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface ILabOrderRepository
{
    Task<LabOrder?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LabOrder?> GetByIdWithTestsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LabOrder>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<LabOrder>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<(IReadOnlyList<LabOrder> Items, int Total)> SearchAsync(string? query, string? status, string? orderType, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken ct = default);
    Task<int> GetCountByStatusAsync(string status, CancellationToken ct = default);
    Task<LabOrder> AddAsync(LabOrder order, CancellationToken ct = default);
    Task UpdateAsync(LabOrder order, CancellationToken ct = default);
}
