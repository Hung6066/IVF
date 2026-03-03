using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Patient?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(string? query, string? gender, int page, int pageSize, CancellationToken ct = default);
    Task<(IReadOnlyList<Patient> Items, int Total)> AdvancedSearchAsync(
        string? query, string? gender, PatientType? patientType, PatientStatus? status,
        PatientPriority? priority, RiskLevel? riskLevel, string? bloodType,
        DateTime? dobFrom, DateTime? dobTo, DateTime? createdFrom, DateTime? createdTo,
        string? sortBy, bool sortDescending, int page, int pageSize, CancellationToken ct = default);
    Task<Patient> AddAsync(Patient patient, CancellationToken ct = default);
    Task UpdateAsync(Patient patient, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);
    // Reporting & analytics
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
    Task<int> GetCountByStatusAsync(PatientStatus status, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPatientsByGenderAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPatientsByTypeAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPatientsByAgeGroupAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPatientsRegistrationTrendAsync(int months, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPatientsByRiskLevelAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Patient>> GetRecentPatientsAsync(int count, CancellationToken ct = default);
    Task<IReadOnlyList<Patient>> GetPatientsRequiringFollowUpAsync(int daysSinceLastVisit, CancellationToken ct = default);
    Task<IReadOnlyList<Patient>> GetExpiredDataRetentionAsync(CancellationToken ct = default);
}
