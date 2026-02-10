using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class DoctorRepository : IDoctorRepository
{
    private readonly IvfDbContext _context;

    public DoctorRepository(IvfDbContext context) => _context = context;

    public async Task<Doctor?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<Doctor?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == userId, ct);

    public async Task<IReadOnlyList<Doctor>> GetBySpecialtyAsync(string specialty, CancellationToken ct = default)
        => await _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.Specialty == specialty)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Doctor>> GetAvailableAsync(CancellationToken ct = default)
        => await _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.IsAvailable)
            .OrderBy(d => d.Specialty)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Doctor>> GetAllAsync(CancellationToken ct = default)
        => await _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .OrderBy(d => d.Specialty)
            .ThenBy(d => d.User.FullName)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Doctor>> SearchAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Doctors.AsNoTracking().Include(d => d.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d => EF.Functions.ILike(d.User.FullName, $"%{search}%"));
        }

        return await query
            .OrderBy(d => d.User.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<Doctor> AddAsync(Doctor doctor, CancellationToken ct = default)
    {
        await _context.Doctors.AddAsync(doctor, ct);
        return doctor;
    }

    public Task UpdateAsync(Doctor doctor, CancellationToken ct = default)
    {
        _context.Doctors.Update(doctor);
        return Task.CompletedTask;
    }
}
