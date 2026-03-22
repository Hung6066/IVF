using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class PatientRepository : IPatientRepository
{
    private readonly IvfDbContext _context;

    public PatientRepository(IvfDbContext context) => _context = context;

    public async Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Patients.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Patient?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _context.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.PatientCode == code, ct);

    public async Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(
        string? query, string? gender, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.Patients.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(gender) && Enum.TryParse<IVF.Domain.Enums.Gender>(gender, true, out var genderEnum))
        {
            q = q.Where(p => p.Gender == genderEnum);
        }

        if (!string.IsNullOrEmpty(query))
            q = q.Where(p => p.FullName.Contains(query) || p.PatientCode.Contains(query) ||
                (p.Phone != null && p.Phone.Contains(query)));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(IReadOnlyList<Patient> Items, int Total)> AdvancedSearchAsync(
        string? query, string? gender, PatientType? patientType, PatientStatus? status,
        PatientPriority? priority, RiskLevel? riskLevel, string? bloodType,
        DateTime? dobFrom, DateTime? dobTo, DateTime? createdFrom, DateTime? createdTo,
        string? sortBy, bool sortDescending, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.Patients.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(gender) && Enum.TryParse<IVF.Domain.Enums.Gender>(gender, true, out var genderEnum))
            q = q.Where(p => p.Gender == genderEnum);

        if (!string.IsNullOrEmpty(query))
            q = q.Where(p => p.FullName.Contains(query) || p.PatientCode.Contains(query) ||
                (p.Phone != null && p.Phone.Contains(query)) ||
                (p.IdentityNumber != null && p.IdentityNumber.Contains(query)) ||
                (p.Email != null && p.Email.Contains(query)));

        if (patientType.HasValue) q = q.Where(p => p.PatientType == patientType.Value);
        if (status.HasValue) q = q.Where(p => p.Status == status.Value);
        if (priority.HasValue) q = q.Where(p => p.Priority == priority.Value);
        if (riskLevel.HasValue) q = q.Where(p => p.RiskLevel == riskLevel.Value);
        if (!string.IsNullOrEmpty(bloodType) && Enum.TryParse<BloodType>(bloodType, true, out var bt))
            q = q.Where(p => p.BloodType == bt);
        if (dobFrom.HasValue) q = q.Where(p => p.DateOfBirth >= dobFrom.Value);
        if (dobTo.HasValue) q = q.Where(p => p.DateOfBirth <= dobTo.Value);
        if (createdFrom.HasValue) q = q.Where(p => p.CreatedAt >= createdFrom.Value);
        if (createdTo.HasValue) q = q.Where(p => p.CreatedAt <= createdTo.Value);

        q = sortBy?.ToLowerInvariant() switch
        {
            "name" => sortDescending ? q.OrderByDescending(p => p.FullName) : q.OrderBy(p => p.FullName),
            "code" => sortDescending ? q.OrderByDescending(p => p.PatientCode) : q.OrderBy(p => p.PatientCode),
            "dob" => sortDescending ? q.OrderByDescending(p => p.DateOfBirth) : q.OrderBy(p => p.DateOfBirth),
            "lastvisit" => sortDescending ? q.OrderByDescending(p => p.LastVisitDate) : q.OrderBy(p => p.LastVisitDate),
            "totalvisits" => sortDescending ? q.OrderByDescending(p => p.TotalVisits) : q.OrderBy(p => p.TotalVisits),
            _ => q.OrderByDescending(p => p.CreatedAt)
        };

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<Patient> AddAsync(Patient patient, CancellationToken ct = default)
    {
        await _context.Patients.AddAsync(patient, ct);
        return patient;
    }

    public Task UpdateAsync(Patient patient, CancellationToken ct = default)
    {
        // When loaded via GetByIdAsync (tracked), EF Core detects only truly changed properties.
        // Only call Update() for detached entities (edge case) to avoid marking all props Modified.
        if (_context.Entry(patient).State == EntityState.Detached)
            _context.Patients.Update(patient);
        return Task.CompletedTask;
    }

    public async Task<string> GenerateCodeAsync(CancellationToken ct = default)
    {
        var count = await _context.Patients.CountAsync(ct);
        return $"BN-{DateTime.Now:yyyy}-{count + 1:D6}";
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
        => await _context.Patients.CountAsync(ct);

    public async Task<int> GetCountByStatusAsync(PatientStatus status, CancellationToken ct = default)
        => await _context.Patients.CountAsync(p => p.Status == status, ct);

    public async Task<Dictionary<string, int>> GetPatientsByGenderAsync(CancellationToken ct = default)
        => await _context.Patients
            .GroupBy(p => p.Gender)
            .Select(g => new { Gender = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Gender, x => x.Count, ct);

    public async Task<Dictionary<string, int>> GetPatientsByTypeAsync(CancellationToken ct = default)
        => await _context.Patients
            .GroupBy(p => p.PatientType)
            .Select(g => new { Type = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, ct);

    public async Task<Dictionary<string, int>> GetPatientsByAgeGroupAsync(CancellationToken ct = default)
    {
        var patients = await _context.Patients
            .Select(p => p.DateOfBirth)
            .ToListAsync(ct);

        var today = DateTime.Today;
        return patients
            .Select(dob =>
            {
                var age = today.Year - dob.Year;
                if (dob.Date > today.AddYears(-age)) age--;
                return age;
            })
            .GroupBy(age => age switch
            {
                < 20 => "<20",
                < 25 => "20-24",
                < 30 => "25-29",
                < 35 => "30-34",
                < 40 => "35-39",
                < 45 => "40-44",
                _ => "45+"
            })
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetPatientsRegistrationTrendAsync(int months, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddMonths(-months);
        var data = await _context.Patients
            .Where(p => p.CreatedAt >= since)
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        return data.ToDictionary(x => $"{x.Year}-{x.Month:D2}", x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetPatientsByRiskLevelAsync(CancellationToken ct = default)
        => await _context.Patients
            .GroupBy(p => p.RiskLevel)
            .Select(g => new { Level = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Level, x => x.Count, ct);

    public async Task<IReadOnlyList<Patient>> GetRecentPatientsAsync(int count, CancellationToken ct = default)
        => await _context.Patients.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Patient>> GetPatientsRequiringFollowUpAsync(int daysSinceLastVisit, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysSinceLastVisit);
        return await _context.Patients.AsNoTracking()
            .Where(p => p.Status == PatientStatus.Active && p.LastVisitDate != null && p.LastVisitDate < cutoff)
            .OrderBy(p => p.LastVisitDate)
            .Take(100)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Patient>> GetExpiredDataRetentionAsync(CancellationToken ct = default)
        => await _context.Patients.AsNoTracking()
            .Where(p => p.DataRetentionExpiryDate != null && p.DataRetentionExpiryDate <= DateTime.UtcNow && !p.IsAnonymized)
            .ToListAsync(ct);
}
