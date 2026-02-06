using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class PatientFingerprint : BaseEntity
{
    public Guid PatientId { get; private set; }
    public byte[] FingerprintData { get; private set; } = Array.Empty<byte>();
    public FingerprintType FingerType { get; private set; }
    public FingerprintSdkType SdkType { get; private set; }
    public int Quality { get; private set; }
    public DateTime CapturedAt { get; private set; } = DateTime.UtcNow;

    // Navigation
    public virtual Patient Patient { get; private set; } = null!;

    private PatientFingerprint() { }

    public static PatientFingerprint Create(
        Guid patientId,
        byte[] fingerprintData,
        FingerprintType fingerType,
        FingerprintSdkType sdkType,
        int quality)
    {
        return new PatientFingerprint
        {
            PatientId = patientId,
            FingerprintData = fingerprintData,
            FingerType = fingerType,
            SdkType = sdkType,
            Quality = quality,
            CapturedAt = DateTime.UtcNow
        };
    }

    public void UpdateFingerprint(byte[] fingerprintData, int quality)
    {
        FingerprintData = fingerprintData;
        Quality = quality;
        CapturedAt = DateTime.UtcNow;
        SetUpdated();
    }
}
