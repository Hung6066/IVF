namespace IVF.Application.Common.Interfaces;

/// <summary>
/// S3-compatible object storage service (MinIO)
/// </summary>
public interface IObjectStorageService
{
    /// <summary>
    /// Upload object to a bucket
    /// </summary>
    Task<ObjectUploadResult> UploadAsync(
        string bucketName, string objectKey, Stream stream,
        string contentType, long size,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Download object from a bucket
    /// </summary>
    Task<Stream?> DownloadAsync(
        string bucketName, string objectKey,
        CancellationToken ct = default);

    /// <summary>
    /// Get presigned download URL (temporary access)
    /// </summary>
    Task<string> GetPresignedUrlAsync(
        string bucketName, string objectKey,
        int expirySeconds = 3600,
        CancellationToken ct = default);

    /// <summary>
    /// Get presigned upload URL (for direct client upload)
    /// </summary>
    Task<string> GetPresignedUploadUrlAsync(
        string bucketName, string objectKey,
        int expirySeconds = 3600,
        CancellationToken ct = default);

    /// <summary>
    /// Delete object from a bucket
    /// </summary>
    Task<bool> DeleteAsync(
        string bucketName, string objectKey,
        CancellationToken ct = default);

    /// <summary>
    /// Check if object exists
    /// </summary>
    Task<bool> ExistsAsync(
        string bucketName, string objectKey,
        CancellationToken ct = default);

    /// <summary>
    /// Copy object within or between buckets
    /// </summary>
    Task CopyAsync(
        string sourceBucket, string sourceKey,
        string destBucket, string destKey,
        CancellationToken ct = default);

    /// <summary>
    /// List objects in a bucket with prefix (folder-like listing)
    /// </summary>
    Task<List<ObjectInfo>> ListObjectsAsync(
        string bucketName, string? prefix = null,
        bool recursive = true,
        CancellationToken ct = default);

    /// <summary>
    /// Get bucket storage statistics
    /// </summary>
    Task<BucketStats> GetBucketStatsAsync(
        string bucketName,
        CancellationToken ct = default);

    /// <summary>
    /// Ensure bucket exists
    /// </summary>
    Task EnsureBucketExistsAsync(
        string bucketName,
        CancellationToken ct = default);
}

public record ObjectUploadResult(
    string BucketName,
    string ObjectKey,
    string ETag,
    long Size,
    string ContentType);

public record ObjectInfo(
    string Key,
    long Size,
    DateTime LastModified,
    string? ContentType,
    string? ETag);

public record BucketStats(
    string BucketName,
    long TotalObjects,
    long TotalSizeBytes);
