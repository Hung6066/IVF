using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// A named backup strategy that defines what to back up (database, MinIO, or both),
/// on what schedule, with optional cloud upload and retention policies.
/// Multiple strategies can coexist (e.g. "Daily DB Only", "Weekly Full", "Hourly MinIO").
/// </summary>
public class DataBackupStrategy : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool Enabled { get; private set; } = true;

    // ─── What to back up ─────────────────────────────────
    public bool IncludeDatabase { get; private set; } = true;
    public bool IncludeMinio { get; private set; } = true;

    // ─── Schedule ────────────────────────────────────────
    public string CronExpression { get; private set; } = "0 2 * * *";

    // ─── Cloud upload ────────────────────────────────────
    public bool UploadToCloud { get; private set; }

    // ─── Retention ───────────────────────────────────────
    public int RetentionDays { get; private set; } = 30;
    public int MaxBackupCount { get; private set; } = 10;

    // ─── Run tracking ────────────────────────────────────
    public DateTime? LastRunAt { get; private set; }
    public string? LastRunOperationCode { get; private set; }
    public string? LastRunStatus { get; private set; }

    private DataBackupStrategy() { }

    public static DataBackupStrategy Create(
        string name,
        string? description,
        bool includeDatabase,
        bool includeMinio,
        string cronExpression,
        bool uploadToCloud,
        int retentionDays,
        int maxBackupCount)
    {
        return new DataBackupStrategy
        {
            Name = name,
            Description = description,
            IncludeDatabase = includeDatabase,
            IncludeMinio = includeMinio,
            CronExpression = cronExpression,
            UploadToCloud = uploadToCloud,
            RetentionDays = retentionDays,
            MaxBackupCount = maxBackupCount
        };
    }

    public void Update(
        string? name = null,
        string? description = null,
        bool? enabled = null,
        bool? includeDatabase = null,
        bool? includeMinio = null,
        string? cronExpression = null,
        bool? uploadToCloud = null,
        int? retentionDays = null,
        int? maxBackupCount = null)
    {
        if (name != null) Name = name;
        if (description != null) Description = description;
        if (enabled.HasValue) Enabled = enabled.Value;
        if (includeDatabase.HasValue) IncludeDatabase = includeDatabase.Value;
        if (includeMinio.HasValue) IncludeMinio = includeMinio.Value;
        if (cronExpression != null) CronExpression = cronExpression;
        if (uploadToCloud.HasValue) UploadToCloud = uploadToCloud.Value;
        if (retentionDays.HasValue) RetentionDays = retentionDays.Value;
        if (maxBackupCount.HasValue) MaxBackupCount = maxBackupCount.Value;
        SetUpdated();
    }

    public void RecordRun(string operationCode, string status)
    {
        LastRunAt = DateTime.UtcNow;
        LastRunOperationCode = operationCode;
        LastRunStatus = status;
        SetUpdated();
    }
}
