using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IEndometriumScanRepository
{
    Task<EndometriumScan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EndometriumScan>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<IReadOnlyList<EndometriumScan>> GetByFetProtocolIdAsync(Guid fetProtocolId, CancellationToken ct = default);
    Task<EndometriumScan> AddAsync(EndometriumScan scan, CancellationToken ct = default);
    Task UpdateAsync(EndometriumScan scan, CancellationToken ct = default);
}
