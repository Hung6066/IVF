using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Persisted cloud backup provider configuration. Single-row table.
/// </summary>
public class CloudBackupConfig : BaseEntity
{
    public string Provider { get; private set; } = "MinIO";
    public bool CompressionEnabled { get; private set; } = true;

    // S3 / MinIO settings
    public string S3Region { get; private set; } = "us-east-1";
    public string S3BucketName { get; private set; } = "ivf-backups";
    public string? S3AccessKey { get; private set; }
    public string? S3SecretKey { get; private set; }
    public string? S3ServiceUrl { get; private set; }
    public bool S3ForcePathStyle { get; private set; } = true;

    // Azure Blob settings
    public string? AzureConnectionString { get; private set; }
    public string AzureContainerName { get; private set; } = "ivf-backups";

    // Google Cloud Storage settings
    public string? GcsProjectId { get; private set; }
    public string GcsBucketName { get; private set; } = "ivf-backups";
    public string? GcsCredentialsPath { get; private set; }

    private CloudBackupConfig() { }

    public static CloudBackupConfig CreateDefault()
    {
        return new CloudBackupConfig();
    }

    public void Update(
        string? provider = null,
        bool? compressionEnabled = null,
        string? s3Region = null,
        string? s3BucketName = null,
        string? s3AccessKey = null,
        string? s3SecretKey = null,
        string? s3ServiceUrl = null,
        bool? s3ForcePathStyle = null,
        string? azureConnectionString = null,
        string? azureContainerName = null,
        string? gcsProjectId = null,
        string? gcsBucketName = null,
        string? gcsCredentialsPath = null)
    {
        if (provider != null) Provider = provider;
        if (compressionEnabled.HasValue) CompressionEnabled = compressionEnabled.Value;
        if (s3Region != null) S3Region = s3Region;
        if (s3BucketName != null) S3BucketName = s3BucketName;
        if (s3AccessKey != null) S3AccessKey = s3AccessKey;
        if (s3SecretKey != null) S3SecretKey = s3SecretKey;
        if (s3ServiceUrl != null) S3ServiceUrl = s3ServiceUrl;
        if (s3ForcePathStyle.HasValue) S3ForcePathStyle = s3ForcePathStyle.Value;
        if (azureConnectionString != null) AzureConnectionString = azureConnectionString;
        if (azureContainerName != null) AzureContainerName = azureContainerName;
        if (gcsProjectId != null) GcsProjectId = gcsProjectId;
        if (gcsBucketName != null) GcsBucketName = gcsBucketName;
        if (gcsCredentialsPath != null) GcsCredentialsPath = gcsCredentialsPath;
        SetUpdated();
    }
}
