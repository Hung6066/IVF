using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class FileTrackingRepository : IFileTrackingRepository
{
    private readonly IvfDbContext _context;
    public FileTrackingRepository(IvfDbContext context) => _context = context;

    public async Task<FileTracking?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _context.FileTrackings
            .Include(f => f.Patient)
            .Include(f => f.Transfers)
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId, ct);

    public async Task<FileTracking?> GetByFileCodeAsync(string fileCode, Guid tenantId, CancellationToken ct = default)
        => await _context.FileTrackings
            .AsNoTracking()
            .Include(f => f.Patient)
            .FirstOrDefaultAsync(f => f.FileCode == fileCode && f.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<FileTracking>> GetByPatientAsync(Guid patientId, Guid tenantId, CancellationToken ct = default)
        => await _context.FileTrackings
            .AsNoTracking()
            .Include(f => f.Transfers)
            .Where(f => f.PatientId == patientId && f.TenantId == tenantId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FileTracking>> GetByLocationAsync(string location, Guid tenantId, CancellationToken ct = default)
        => await _context.FileTrackings
            .AsNoTracking()
            .Include(f => f.Patient)
            .Where(f => f.CurrentLocation == location && f.TenantId == tenantId)
            .OrderBy(f => f.FileCode)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FileTracking>> GetByStatusAsync(FileStatus status, Guid tenantId, CancellationToken ct = default)
        => await _context.FileTrackings
            .AsNoTracking()
            .Include(f => f.Patient)
            .Where(f => f.Status == status && f.TenantId == tenantId)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<FileTracking> Items, int Total)> SearchAsync(string? query, FileStatus? status, string? location, int page, int pageSize, Guid tenantId, CancellationToken ct = default)
    {
        var q = _context.FileTrackings.AsNoTracking()
            .Include(f => f.Patient)
            .Where(f => f.TenantId == tenantId);

        if (!string.IsNullOrEmpty(query))
            q = q.Where(f => f.FileCode.Contains(query) || f.Patient!.FullName.Contains(query));

        if (status.HasValue)
            q = q.Where(f => f.Status == status.Value);

        if (!string.IsNullOrEmpty(location))
            q = q.Where(f => f.CurrentLocation.Contains(location));

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(f => f.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<bool> FileCodeExistsAsync(string fileCode, Guid tenantId, CancellationToken ct = default)
        => await _context.FileTrackings.AnyAsync(f => f.FileCode == fileCode && f.TenantId == tenantId, ct);

    public async Task AddAsync(FileTracking file, CancellationToken ct = default)
        => await _context.FileTrackings.AddAsync(file, ct);

    public Task UpdateAsync(FileTracking file, CancellationToken ct = default)
    {
        _context.FileTrackings.Update(file);
        return Task.CompletedTask;
    }
}
