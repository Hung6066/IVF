using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Persisted cloud/external replication configuration for PostgreSQL and MinIO.
/// Single-row table — stores remote target connection details, SSL settings, and sync status.
/// </summary>
public class CloudReplicationConfig : BaseEntity
{
    // ─── PostgreSQL Remote Replication ────────────────────
    public bool DbReplicationEnabled { get; private set; }
    public string? RemoteDbHost { get; private set; }
    public int RemoteDbPort { get; private set; } = 5432;
    public string? RemoteDbUser { get; private set; }
    public string? RemoteDbPassword { get; private set; }
    public string RemoteDbSslMode { get; private set; } = "require";
    public string? RemoteDbSlotName { get; private set; } = "cloud_standby_slot";
    public string? RemoteDbAllowedIps { get; private set; }

    // ─── MinIO / S3-compatible Remote Replication ─────────
    public bool MinioReplicationEnabled { get; private set; }
    public string? RemoteMinioEndpoint { get; private set; }
    public string? RemoteMinioAccessKey { get; private set; }
    public string? RemoteMinioSecretKey { get; private set; }
    public string RemoteMinioBucket { get; private set; } = "ivf-replica";
    public bool RemoteMinioUseSsl { get; private set; } = true;
    public string RemoteMinioRegion { get; private set; } = "us-east-1";
    public string RemoteMinioSyncMode { get; private set; } = "incremental"; // incremental | full
    public string? RemoteMinioSyncCron { get; private set; } = "0 */2 * * *"; // every 2 hours

    // ─── Status Tracking ─────────────────────────────────
    public DateTime? LastDbSyncAt { get; private set; }
    public DateTime? LastMinioSyncAt { get; private set; }
    public string? LastDbSyncStatus { get; private set; }
    public string? LastMinioSyncStatus { get; private set; }
    public long LastMinioSyncBytes { get; private set; }
    public int LastMinioSyncFiles { get; private set; }

    private CloudReplicationConfig() { }

    public static CloudReplicationConfig CreateDefault() => new();

    public void UpdateDbSettings(
        bool? enabled = null,
        string? remoteHost = null,
        int? remotePort = null,
        string? remoteUser = null,
        string? remotePassword = null,
        string? sslMode = null,
        string? slotName = null,
        string? allowedIps = null)
    {
        if (enabled.HasValue) DbReplicationEnabled = enabled.Value;
        if (remoteHost != null) RemoteDbHost = remoteHost;
        if (remotePort.HasValue) RemoteDbPort = remotePort.Value;
        if (remoteUser != null) RemoteDbUser = remoteUser;
        if (remotePassword != null) RemoteDbPassword = remotePassword;
        if (sslMode != null) RemoteDbSslMode = sslMode;
        if (slotName != null) RemoteDbSlotName = slotName;
        if (allowedIps != null) RemoteDbAllowedIps = allowedIps;
        SetUpdated();
    }

    public void UpdateMinioSettings(
        bool? enabled = null,
        string? endpoint = null,
        string? accessKey = null,
        string? secretKey = null,
        string? bucket = null,
        bool? useSsl = null,
        string? region = null,
        string? syncMode = null,
        string? syncCron = null)
    {
        if (enabled.HasValue) MinioReplicationEnabled = enabled.Value;
        if (endpoint != null) RemoteMinioEndpoint = endpoint;
        if (accessKey != null) RemoteMinioAccessKey = accessKey;
        if (secretKey != null) RemoteMinioSecretKey = secretKey;
        if (bucket != null) RemoteMinioBucket = bucket;
        if (useSsl.HasValue) RemoteMinioUseSsl = useSsl.Value;
        if (region != null) RemoteMinioRegion = region;
        if (syncMode != null) RemoteMinioSyncMode = syncMode;
        if (syncCron != null) RemoteMinioSyncCron = syncCron;
        SetUpdated();
    }

    public void RecordDbSync(string status)
    {
        LastDbSyncAt = DateTime.UtcNow;
        LastDbSyncStatus = status;
        SetUpdated();
    }

    public void RecordMinioSync(string status, long bytes = 0, int files = 0)
    {
        LastMinioSyncAt = DateTime.UtcNow;
        LastMinioSyncStatus = status;
        LastMinioSyncBytes = bytes;
        LastMinioSyncFiles = files;
        SetUpdated();
    }
}
