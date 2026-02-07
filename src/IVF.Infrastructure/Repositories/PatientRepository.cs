using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
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
        => await _context.Patients.FirstOrDefaultAsync(p => p.PatientCode == code, ct);

    public async Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(
        string? query, string? gender, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.Patients.AsQueryable();
        
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

    public async Task<Patient> AddAsync(Patient patient, CancellationToken ct = default)
    {
        await _context.Patients.AddAsync(patient, ct);
        return patient;
    }

    public Task UpdateAsync(Patient patient, CancellationToken ct = default)
    {
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
}
