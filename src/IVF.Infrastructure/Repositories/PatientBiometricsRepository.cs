using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class PatientBiometricsRepository : IPatientBiometricsRepository
{
    private readonly IvfDbContext _context;

    public PatientBiometricsRepository(IvfDbContext context)
    {
        _context = context;
    }

    // ==================== Photo ====================
    public async Task<PatientPhoto?> GetPhotoByPatientIdAsync(Guid patientId, CancellationToken ct = default)
    {
        return await _context.PatientPhotos
            .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);
    }

    public async Task AddPhotoAsync(PatientPhoto photo, CancellationToken ct = default)
    {
        await _context.PatientPhotos.AddAsync(photo, ct);
    }

    public Task UpdatePhotoAsync(PatientPhoto photo, CancellationToken ct = default)
    {
        _context.PatientPhotos.Update(photo);
        return Task.CompletedTask;
    }

    public Task DeletePhotoAsync(PatientPhoto photo, CancellationToken ct = default)
    {
        _context.PatientPhotos.Remove(photo);
        return Task.CompletedTask;
    }

    // ==================== Fingerprint ====================
    public async Task<IReadOnlyList<PatientFingerprint>> GetFingerprintsByPatientIdAsync(Guid patientId, CancellationToken ct = default)
    {
        return await _context.PatientFingerprints
            .Where(f => f.PatientId == patientId)
            .OrderBy(f => f.FingerType)
            .ToListAsync(ct);
    }

    public async Task<PatientFingerprint?> GetFingerprintByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.PatientFingerprints
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<PatientFingerprint?> GetFingerprintByPatientAndTypeAsync(Guid patientId, FingerprintType fingerType, CancellationToken ct = default)
    {
        return await _context.PatientFingerprints
            .FirstOrDefaultAsync(f => f.PatientId == patientId && f.FingerType == fingerType, ct);
    }

    public async Task AddFingerprintAsync(PatientFingerprint fingerprint, CancellationToken ct = default)
    {
        await _context.PatientFingerprints.AddAsync(fingerprint, ct);
    }

    public Task UpdateFingerprintAsync(PatientFingerprint fingerprint, CancellationToken ct = default)
    {
        _context.PatientFingerprints.Update(fingerprint);
        return Task.CompletedTask;
    }

    public Task DeleteFingerprintAsync(PatientFingerprint fingerprint, CancellationToken ct = default)
    {
        _context.PatientFingerprints.Remove(fingerprint);
        return Task.CompletedTask;
    }

    // ==================== Fingerprint Matching ====================
    public async Task<IReadOnlyList<PatientFingerprint>> GetAllFingerprintsBySdkTypeAsync(FingerprintSdkType sdkType, CancellationToken ct = default)
    {
        return await _context.PatientFingerprints
            .Include(f => f.Patient)
            .Where(f => f.SdkType == sdkType)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PatientFingerprint>> GetAllFingerprintsAsync(CancellationToken ct = default)
    {
        return await _context.PatientFingerprints
            .AsNoTracking() // Performance optimization for read-only
            .ToListAsync(ct);
    }
}
