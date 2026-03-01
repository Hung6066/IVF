using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Persisted rotation schedule for automated secret rotation.
/// </summary>
public class SecretRotationSchedule : BaseEntity
{
    public string SecretPath { get; private set; } = string.Empty;
    public int RotationIntervalDays { get; private set; }
    public int GracePeriodHours { get; private set; } = 24;
    public bool AutomaticallyRotate { get; private set; } = true;
    public string RotationStrategy { get; private set; } = "generate";
    public string? CallbackUrl { get; private set; }
    public DateTime? LastRotatedAt { get; private set; }
    public DateTime NextRotationAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid? CreatedBy { get; private set; }

    private SecretRotationSchedule() { }

    public static SecretRotationSchedule Create(
        string secretPath,
        int rotationIntervalDays,
        int gracePeriodHours = 24,
        bool automaticallyRotate = true,
        string? rotationStrategy = null,
        string? callbackUrl = null,
        Guid? createdBy = null)
    {
        return new SecretRotationSchedule
        {
            SecretPath = secretPath,
            RotationIntervalDays = rotationIntervalDays,
            GracePeriodHours = gracePeriodHours,
            AutomaticallyRotate = automaticallyRotate,
            RotationStrategy = rotationStrategy ?? "generate",
            CallbackUrl = callbackUrl,
            NextRotationAt = DateTime.UtcNow.AddDays(rotationIntervalDays),
            CreatedBy = createdBy,
        };
    }

    public void RecordRotation()
    {
        LastRotatedAt = DateTime.UtcNow;
        NextRotationAt = DateTime.UtcNow.AddDays(RotationIntervalDays);
        SetUpdated();
    }

    public void UpdateConfig(int rotationIntervalDays, int gracePeriodHours, bool automaticallyRotate)
    {
        RotationIntervalDays = rotationIntervalDays;
        GracePeriodHours = gracePeriodHours;
        AutomaticallyRotate = automaticallyRotate;
        // Recalculate next rotation from last rotation or now
        NextRotationAt = (LastRotatedAt ?? DateTime.UtcNow).AddDays(rotationIntervalDays);
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public bool IsDueForRotation => IsActive && AutomaticallyRotate && DateTime.UtcNow >= NextRotationAt;
}
