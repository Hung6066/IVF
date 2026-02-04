using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IDoctorRepository
{
    Task<Doctor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Doctor?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Doctor>> GetBySpecialtyAsync(string specialty, CancellationToken ct = default);
    Task<IReadOnlyList<Doctor>> GetAvailableAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Doctor>> GetAllAsync(CancellationToken ct = default);
    Task<Doctor> AddAsync(Doctor doctor, CancellationToken ct = default);
    Task UpdateAsync(Doctor doctor, CancellationToken ct = default);
}
