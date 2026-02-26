namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Abstraction for cloud backup storage providers (AWS S3, Azure Blob, Google Cloud Storage, MinIO).
/// </summary>
public interface ICloudBackupProvider
{
    /// <summary>Provider name for display (e.g. "AWS S3", "Azure Blob", "Google Cloud Storage")</summary>
    string ProviderName { get; }

    /// <summary>Upload a local backup file to cloud storage</summary>
    Task<CloudBackupResult> UploadAsync(string localFilePath, string objectKey, CancellationToken ct = default);

    /// <summary>Download a backup from cloud storage to a local file</summary>
    Task<string> DownloadAsync(string objectKey, string localDirectory, CancellationToken ct = default);

    /// <summary>List all backup objects in the cloud container/bucket</summary>
    Task<List<CloudBackupObject>> ListAsync(CancellationToken ct = default);

    /// <summary>Delete a backup from cloud storage</summary>
    Task<bool> DeleteAsync(string objectKey, CancellationToken ct = default);

    /// <summary>Check if a backup exists in cloud storage</summary>
    Task<bool> ExistsAsync(string objectKey, CancellationToken ct = default);

    /// <summary>Test connectivity to the cloud provider</summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}

public record CloudBackupResult(string ObjectKey, long SizeBytes, string? ETag);

public record CloudBackupObject(
    string ObjectKey,
    string FileName,
    long SizeBytes,
    DateTime LastModified,
    string? ETag);
