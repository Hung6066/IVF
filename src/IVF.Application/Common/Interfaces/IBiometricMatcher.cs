using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IBiometricMatcher
{
    bool IsLoaded { get; }
    Task SyncToRedis(Guid patientId, FingerprintType fingerType, byte[] fingerprintData);
    (bool Match, Guid? PatientId, int Score) Identify(byte[] featureSetData);
}
