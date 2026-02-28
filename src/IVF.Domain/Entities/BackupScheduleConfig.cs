using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Persisted backup schedule configuration. Single-row table.
/// </summary>
public class BackupScheduleConfig : BaseEntity
{
    public bool Enabled { get; private set; } = true;
    public string CronExpression { get; private set; } = "0 2 * * *";
    public bool KeysOnly { get; private set; }
    public int RetentionDays { get; private set; } = 90;
    public int MaxBackupCount { get; private set; } = 30;
    public bool CloudSyncEnabled { get; private set; }
    public DateTime? LastScheduledRun { get; private set; }
    public string? LastScheduledOperationCode { get; private set; }

    private BackupScheduleConfig() { }

    public static BackupScheduleConfig CreateDefault()
    {
        return new BackupScheduleConfig();
    }

    public void Update(
        bool? enabled = null,
        string? cronExpression = null,
        bool? keysOnly = null,
        int? retentionDays = null,
        int? maxBackupCount = null,
        bool? cloudSyncEnabled = null)
    {
        if (enabled.HasValue) Enabled = enabled.Value;
        if (cronExpression != null) CronExpression = cronExpression;
        if (keysOnly.HasValue) KeysOnly = keysOnly.Value;
        if (retentionDays.HasValue) RetentionDays = retentionDays.Value;
        if (maxBackupCount.HasValue) MaxBackupCount = maxBackupCount.Value;
        if (cloudSyncEnabled.HasValue) CloudSyncEnabled = cloudSyncEnabled.Value;
        SetUpdated();
    }

    public void RecordScheduledRun(string operationCode)
    {
        LastScheduledRun = DateTime.UtcNow;
        LastScheduledOperationCode = operationCode;
        SetUpdated();
    }
}
