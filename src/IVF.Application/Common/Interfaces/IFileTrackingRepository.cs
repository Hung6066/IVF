using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IFileTrackingRepository
{
    Task<FileTracking?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<FileTracking?> GetByFileCodeAsync(string fileCode, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<FileTracking>> GetByPatientAsync(Guid patientId, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<FileTracking>> GetByLocationAsync(string location, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<FileTracking>> GetByStatusAsync(FileStatus status, Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<FileTracking> Items, int Total)> SearchAsync(string? query, FileStatus? status, string? location, int page, int pageSize, Guid tenantId, CancellationToken ct = default);
    Task<bool> FileCodeExistsAsync(string fileCode, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(FileTracking file, CancellationToken ct = default);
    Task UpdateAsync(FileTracking file, CancellationToken ct = default);
}
