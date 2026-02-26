namespace IVF.API.Services;

/// <summary>
/// Configuration for multi-cloud backup storage.
/// Supports AWS S3, Azure Blob Storage, Google Cloud Storage, and MinIO (S3-compatible).
/// </summary>
public sealed class CloudBackupSettings
{
    public const string SectionName = "CloudBackup";

    /// <summary>Active provider: "S3", "Azure", "GCS", "MinIO" (default)</summary>
    public string Provider { get; set; } = "MinIO";

    /// <summary>Enable compression before cloud upload</summary>
    public bool CompressionEnabled { get; set; } = true;

    /// <summary>AWS S3 / S3-compatible (MinIO, DigitalOcean Spaces)</summary>
    public S3Settings S3 { get; set; } = new();

    /// <summary>Azure Blob Storage</summary>
    public AzureBlobSettings Azure { get; set; } = new();

    /// <summary>Google Cloud Storage</summary>
    public GcsSettings Gcs { get; set; } = new();
}

public sealed class S3Settings
{
    public string Region { get; set; } = "us-east-1";
    public string BucketName { get; set; } = "ivf-backups";
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    /// <summary>Override service URL for S3-compatible providers (MinIO, etc.)</summary>
    public string? ServiceUrl { get; set; }
    public bool ForcePathStyle { get; set; } = true;
}

public sealed class AzureBlobSettings
{
    public string? ConnectionString { get; set; }
    public string ContainerName { get; set; } = "ivf-backups";
}

public sealed class GcsSettings
{
    public string? ProjectId { get; set; }
    public string BucketName { get; set; } = "ivf-backups";
    /// <summary>Path to service account JSON key file</summary>
    public string? CredentialsPath { get; set; }
}
