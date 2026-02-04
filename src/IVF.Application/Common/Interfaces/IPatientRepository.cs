using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Patient?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default);
    Task<Patient> AddAsync(Patient patient, CancellationToken ct = default);
    Task UpdateAsync(Patient patient, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);
    // Reporting
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
}
