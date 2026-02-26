using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public enum BackupOperationType { Backup, Restore }
public enum BackupOperationStatus { Running, Completed, Failed, Cancelled }

/// <summary>
/// Persisted record of a backup or restore operation, including all log lines.
/// </summary>
public class BackupOperation : BaseEntity
{
    public string OperationCode { get; private set; } = string.Empty;
    public BackupOperationType Type { get; private set; }
    public BackupOperationStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ArchivePath { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? StartedBy { get; private set; }

    // Log lines stored as JSONB for efficient storage
    public string? LogLinesJson { get; private set; }

    /// <summary>MinIO object key if uploaded to cloud</summary>
    public string? CloudStorageKey { get; private set; }
    public DateTime? CloudUploadedAt { get; private set; }

    private BackupOperation() { }

    public static BackupOperation Create(
        string operationCode,
        BackupOperationType type,
        string? startedBy)
    {
        return new BackupOperation
        {
            OperationCode = operationCode,
            Type = type,
            Status = BackupOperationStatus.Running,
            StartedAt = DateTime.UtcNow,
            StartedBy = startedBy
        };
    }

    public void MarkCompleted(string? archivePath)
    {
        Status = BackupOperationStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        ArchivePath = archivePath;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = BackupOperationStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }

    public void MarkCancelled()
    {
        Status = BackupOperationStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }

    public void SetArchivePath(string path)
    {
        ArchivePath = path;
    }

    public void UpdateLogLines(string logLinesJson)
    {
        LogLinesJson = logLinesJson;
    }

    public void SetCloudUploaded(string objectKey)
    {
        CloudStorageKey = objectKey;
        CloudUploadedAt = DateTime.UtcNow;
    }
}
