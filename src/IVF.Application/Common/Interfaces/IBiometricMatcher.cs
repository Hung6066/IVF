using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IBiometricMatcher
{
    Task SyncToRedis(Guid patientId, FingerprintType fingerType, byte[] fingerprintData);
}
