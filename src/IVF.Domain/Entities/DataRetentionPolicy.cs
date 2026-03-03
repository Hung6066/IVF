using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Data retention policy for automated purging.
/// HIPAA requires 7-year retention for medical audit data; GDPR requires data minimization.
/// </summary>
public class DataRetentionPolicy : BaseEntity
{
    public string EntityType { get; private set; } = string.Empty; // "SecurityEvent", "UserLoginHistory", "AuditLog", "UserSession"
    public int RetentionDays { get; private set; }
    public string Action { get; private set; } = "Delete"; // Delete, Anonymize, Archive
    public bool IsEnabled { get; private set; } = true;
    public DateTime? LastExecutedAt { get; private set; }
    public int? LastPurgedCount { get; private set; }
    public Guid? CreatedBy { get; private set; }

    private DataRetentionPolicy() { }

    public static DataRetentionPolicy Create(
        string entityType,
        int retentionDays,
        string action,
        Guid? createdBy)
    {
        return new DataRetentionPolicy
        {
            EntityType = entityType,
            RetentionDays = retentionDays,
            Action = action,
            CreatedBy = createdBy
        };
    }

    public void Update(int retentionDays, string action, bool isEnabled)
    {
        RetentionDays = retentionDays;
        Action = action;
        IsEnabled = isEnabled;
        SetUpdated();
    }

    public void RecordExecution(int purgedCount)
    {
        LastExecutedAt = DateTime.UtcNow;
        LastPurgedCount = purgedCount;
        SetUpdated();
    }
}
