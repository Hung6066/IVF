using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class ApiKeyManagement : BaseEntity
{
    public string KeyName { get; private set; } = string.Empty;
    public string ServiceName { get; private set; } = string.Empty;
    public string? KeyPrefix { get; private set; }
    public string KeyHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public string? Environment { get; private set; }
    public Guid CreatedBy { get; private set; }
    public int? RotationIntervalDays { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? LastRotatedAt { get; private set; }
    public int Version { get; private set; } = 1;

    private ApiKeyManagement() { }

    public static ApiKeyManagement Create(
        string keyName,
        string serviceName,
        string? keyPrefix,
        string keyHash,
        string? environment,
        Guid createdBy,
        int? rotationIntervalDays = 90)
    {
        return new ApiKeyManagement
        {
            KeyName = keyName,
            ServiceName = serviceName,
            KeyPrefix = keyPrefix,
            KeyHash = keyHash,
            Environment = environment,
            CreatedBy = createdBy,
            RotationIntervalDays = rotationIntervalDays
        };
    }

    public void Rotate(string newKeyHash)
    {
        KeyHash = newKeyHash;
        LastRotatedAt = DateTime.UtcNow;
        Version++;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }
}
