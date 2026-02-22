using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace IVF.Infrastructure.Services;

/// <summary>
/// MinIO S3-compatible object storage implementation
/// Lưu trữ hồ sơ bệnh án điện tử theo cấu trúc:
///   {bucket}/{patientCode}/{documentType}/{year}/{filename}
/// </summary>
public class MinioObjectStorageService : IObjectStorageService
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioObjectStorageService> _logger;

    public MinioObjectStorageService(
        IMinioClient client,
        IOptions<MinioOptions> options,
        ILogger<MinioObjectStorageService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ObjectUploadResult> UploadAsync(
        string bucketName, string objectKey, Stream stream,
        string contentType, long size,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(bucketName, ct);

        var putArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(size)
            .WithContentType(contentType);

        if (metadata != null && metadata.Count > 0)
        {
            putArgs.WithHeaders(metadata);
        }

        var response = await _client.PutObjectAsync(putArgs, ct);

        _logger.LogInformation(
            "Uploaded {ObjectKey} to {Bucket} ({Size} bytes)",
            objectKey, bucketName, size);

        return new ObjectUploadResult(
            bucketName, objectKey, response.Etag, size, contentType);
    }

    public async Task<Stream?> DownloadAsync(
        string bucketName, string objectKey,
        CancellationToken ct = default)
    {
        try
        {
            var memoryStream = new MemoryStream();

            var getArgs = new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithCallbackStream(async (stream, cancellationToken) =>
                {
                    await stream.CopyToAsync(memoryStream, cancellationToken);
                });

            await _client.GetObjectAsync(getArgs, ct);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download {ObjectKey} from {Bucket}", objectKey, bucketName);
            return null;
        }
    }

    public async Task<string> GetPresignedUrlAsync(
        string bucketName, string objectKey,
        int expirySeconds = 3600,
        CancellationToken ct = default)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithExpiry(expirySeconds);

        return await _client.PresignedGetObjectAsync(args);
    }

    public async Task<string> GetPresignedUploadUrlAsync(
        string bucketName, string objectKey,
        int expirySeconds = 3600,
        CancellationToken ct = default)
    {
        var args = new PresignedPutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithExpiry(expirySeconds);

        return await _client.PresignedPutObjectAsync(args);
    }

    public async Task<bool> DeleteAsync(
        string bucketName, string objectKey,
        CancellationToken ct = default)
    {
        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey);

            await _client.RemoveObjectAsync(args, ct);
            _logger.LogInformation("Deleted {ObjectKey} from {Bucket}", objectKey, bucketName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete {ObjectKey} from {Bucket}", objectKey, bucketName);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(
        string bucketName, string objectKey,
        CancellationToken ct = default)
    {
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey);

            await _client.StatObjectAsync(args, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task CopyAsync(
        string sourceBucket, string sourceKey,
        string destBucket, string destKey,
        CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(destBucket, ct);

        var copySource = new CopySourceObjectArgs()
            .WithBucket(sourceBucket)
            .WithObject(sourceKey);

        var args = new CopyObjectArgs()
            .WithBucket(destBucket)
            .WithObject(destKey)
            .WithCopyObjectSource(copySource);

        await _client.CopyObjectAsync(args, ct);

        _logger.LogInformation(
            "Copied {SourceKey} from {SourceBucket} to {DestKey} in {DestBucket}",
            sourceKey, sourceBucket, destKey, destBucket);
    }

    public async Task<List<ObjectInfo>> ListObjectsAsync(
        string bucketName, string? prefix = null,
        bool recursive = true,
        CancellationToken ct = default)
    {
        var result = new List<ObjectInfo>();

        var args = new ListObjectsArgs()
            .WithBucket(bucketName)
            .WithRecursive(recursive);

        if (!string.IsNullOrEmpty(prefix))
            args.WithPrefix(prefix);

        var tcs = new TaskCompletionSource<List<ObjectInfo>>();
        var observable = _client.ListObjectsEnumAsync(args, ct);

        await foreach (var item in observable.WithCancellation(ct))
        {
            result.Add(new ObjectInfo(
                item.Key,
                (long)item.Size,
                item.LastModifiedDateTime ?? DateTime.MinValue,
                item.ContentType,
                item.ETag));
        }

        return result;
    }

    public async Task<BucketStats> GetBucketStatsAsync(
        string bucketName,
        CancellationToken ct = default)
    {
        var objects = await ListObjectsAsync(bucketName, recursive: true, ct: ct);
        return new BucketStats(
            bucketName,
            objects.Count,
            objects.Sum(o => o.Size));
    }

    public async Task EnsureBucketExistsAsync(
        string bucketName,
        CancellationToken ct = default)
    {
        var beArgs = new BucketExistsArgs().WithBucket(bucketName);
        if (!await _client.BucketExistsAsync(beArgs, ct))
        {
            var mbArgs = new MakeBucketArgs().WithBucket(bucketName);
            await _client.MakeBucketAsync(mbArgs, ct);
            _logger.LogInformation("Created bucket: {Bucket}", bucketName);
        }
    }
}

/// <summary>
/// Extension to convert IObservable to IAsyncEnumerable
/// </summary>
internal static class ObservableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IObservable<T> source)
    {
        var queue = new System.Collections.Concurrent.ConcurrentQueue<T>();
        var tcs = new TaskCompletionSource();
        Exception? error = null;

        source.Subscribe(
            onNext: item => queue.Enqueue(item),
            onError: ex => { error = ex; tcs.TrySetResult(); },
            onCompleted: () => tcs.TrySetResult());

        while (!tcs.Task.IsCompleted || !queue.IsEmpty)
        {
            if (queue.TryDequeue(out var item))
            {
                yield return item;
            }
            else if (!tcs.Task.IsCompleted)
            {
                await Task.Delay(10);
            }
        }

        if (error != null)
            throw error;
    }
}
