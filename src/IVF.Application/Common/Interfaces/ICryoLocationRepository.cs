using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface ICryoLocationRepository
{
    Task<CryoLocation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CryoLocation>> GetAvailableAsync(CancellationToken ct = default);
    Task<CryoLocation> AddAsync(CryoLocation location, CancellationToken ct = default);
    Task<IReadOnlyList<CryoStatsDto>> GetStorageStatsAsync(CancellationToken ct = default);
    Task UpdateAsync(CryoLocation location, CancellationToken ct = default);
    Task<bool> TankExistsAsync(string tank, CancellationToken ct = default);
    Task DeleteTankAsync(string tank, CancellationToken ct = default);
    Task<Dictionary<SpecimenType, int>> GetSpecimenCountsAsync(CancellationToken ct = default);
    Task SetTankOccupancyAsync(string tank, int occupiedCount, SpecimenType type, CancellationToken ct = default);
}

public record CryoStatsDto(string Tank, int CanisterCount, int CaneCount, int GobletCount, int Available, int Used, int SpecimenType);
