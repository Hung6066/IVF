using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IUltrasoundRepository
{
    Task<Ultrasound?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Ultrasound>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<Ultrasound> AddAsync(Ultrasound ultrasound, CancellationToken ct = default);
    Task UpdateAsync(Ultrasound ultrasound, CancellationToken ct = default);
}

public interface IEmbryoRepository
{
    Task<Embryo?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Embryo>> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task<Embryo> AddAsync(Embryo embryo, CancellationToken ct = default);
    Task UpdateAsync(Embryo embryo, CancellationToken ct = default);
    Task<int> GetNextNumberForCycleAsync(Guid cycleId, CancellationToken ct = default);
    Task<IReadOnlyList<Embryo>> GetActiveAsync(CancellationToken ct = default);
}

public interface ICryoLocationRepository
{
    Task<CryoLocation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CryoLocation>> GetAvailableAsync(CancellationToken ct = default);
    Task<CryoLocation> AddAsync(CryoLocation location, CancellationToken ct = default);
    Task<IReadOnlyList<CryoStatsDto>> GetStorageStatsAsync(CancellationToken ct = default);
    Task UpdateAsync(CryoLocation location, CancellationToken ct = default);
}

public record CryoStatsDto(string Tank, int CanisterCount, int CaneCount, int GobletCount, int Available, int Used);
