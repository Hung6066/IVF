using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IPatientBiometricsRepository
{
    // Photo operations
    Task<PatientPhoto?> GetPhotoByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task AddPhotoAsync(PatientPhoto photo, CancellationToken ct = default);
    Task UpdatePhotoAsync(PatientPhoto photo, CancellationToken ct = default);
    Task DeletePhotoAsync(PatientPhoto photo, CancellationToken ct = default);

    // Fingerprint operations
    Task<IReadOnlyList<PatientFingerprint>> GetFingerprintsByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<PatientFingerprint?> GetFingerprintByIdAsync(Guid id, CancellationToken ct = default);
    Task<PatientFingerprint?> GetFingerprintByPatientAndTypeAsync(Guid patientId, FingerprintType fingerType, CancellationToken ct = default);
    Task AddFingerprintAsync(PatientFingerprint fingerprint, CancellationToken ct = default);
    Task UpdateFingerprintAsync(PatientFingerprint fingerprint, CancellationToken ct = default);
    Task DeleteFingerprintAsync(PatientFingerprint fingerprint, CancellationToken ct = default);

    // Fingerprint matching
    Task<IReadOnlyList<PatientFingerprint>> GetAllFingerprintsBySdkTypeAsync(FingerprintSdkType sdkType, CancellationToken ct = default);
    Task<IReadOnlyList<PatientFingerprint>> GetAllFingerprintsAsync(CancellationToken ct = default);
}
