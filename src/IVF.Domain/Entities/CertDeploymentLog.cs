using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Persistent log of certificate deployment operations with per-step detail.
/// </summary>
public class CertDeploymentLog : BaseEntity
{
    public Guid CertificateId { get; set; }
    public ManagedCertificate Certificate { get; set; } = null!;

    /// <summary>Unique operation ID used for SignalR group subscription.</summary>
    public string OperationId { get; set; } = string.Empty;

    /// <summary>Target: pg-primary, pg-replica, minio-primary, minio-replica, custom</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Container name (ivf-db, ivf-minio, etc.)</summary>
    public string Container { get; set; } = string.Empty;

    /// <summary>Remote host if SSH-based deploy, null for local.</summary>
    public string? RemoteHost { get; set; }

    public DeployStatus Status { get; set; } = DeployStatus.Running;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>JSON array of step log lines for persistence.</summary>
    public List<DeployLogLine> LogLines { get; set; } = [];

    public void AddLine(string level, string message)
    {
        LogLines.Add(new DeployLogLine
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message
        });
    }

    public void Complete(bool success, string? error = null)
    {
        Status = success ? DeployStatus.Completed : DeployStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = error;
    }
}

public class DeployLogLine
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "info"; // info, warn, error, success
    public string Message { get; set; } = string.Empty;
}

public enum DeployStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2
}
