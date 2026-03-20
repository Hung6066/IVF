using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IProcedureRepository
{
    Task<Procedure?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Procedure>> GetByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<Procedure>> GetByCycleAsync(Guid cycleId, CancellationToken ct = default);
    Task<IReadOnlyList<Procedure>> GetByDateAsync(DateTime date, CancellationToken ct = default);
    Task<IReadOnlyList<Procedure>> SearchAsync(string? query = null, string? procedureType = null, string? status = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<int> CountAsync(string? query = null, string? procedureType = null, string? status = null, CancellationToken ct = default);
    Task<Procedure> AddAsync(Procedure procedure, CancellationToken ct = default);
    Task UpdateAsync(Procedure procedure, CancellationToken ct = default);
}
