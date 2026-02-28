using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using IVF.Application.Common.Interfaces;

namespace IVF.API.Services.CloudProviders;

/// <summary>
/// AWS S3 / S3-compatible (MinIO, DigitalOcean Spaces) cloud backup provider.
/// </summary>
public sealed class S3CloudBackupProvider : ICloudBackupProvider, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucketName;
    private readonly ILogger<S3CloudBackupProvider> _logger;

    public string ProviderName { get; }

    public S3CloudBackupProvider(S3Settings settings, ILogger<S3CloudBackupProvider> logger, string providerName = "AWS S3")
    {
        _logger = logger;
        _bucketName = settings.BucketName;
        ProviderName = providerName;

        var config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(settings.Region),
            ForcePathStyle = settings.ForcePathStyle
        };

        if (!string.IsNullOrEmpty(settings.ServiceUrl))
            config.ServiceURL = settings.ServiceUrl;

        // Accept self-signed certs for internal MinIO TLS
        if (settings.ServiceUrl?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true)
        {
            config.HttpClientFactory = new SelfSignedHttpClientFactory();
        }

        _client = !string.IsNullOrEmpty(settings.AccessKey)
            ? new AmazonS3Client(settings.AccessKey, settings.SecretKey, config)
            : new AmazonS3Client(config);
    }

    public async Task<CloudBackupResult> UploadAsync(string localFilePath, string objectKey, CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(ct);

        using var transferUtility = new TransferUtility(_client);
        var request = new TransferUtilityUploadRequest
        {
            FilePath = localFilePath,
            BucketName = _bucketName,
            Key = objectKey,
            ContentType = "application/octet-stream"
        };

        await transferUtility.UploadAsync(request, ct);

        var meta = await _client.GetObjectMetadataAsync(_bucketName, objectKey, ct);
        _logger.LogInformation("Uploaded {Key} to S3 ({Size:N0} bytes)", objectKey, meta.ContentLength);

        return new CloudBackupResult(objectKey, meta.ContentLength, meta.ETag);
    }

    public async Task<string> DownloadAsync(string objectKey, string localDirectory, CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(ct);

        var localPath = Path.Combine(localDirectory, Path.GetFileName(objectKey));
        Directory.CreateDirectory(localDirectory);

        using var transferUtility = new TransferUtility(_client);
        await transferUtility.DownloadAsync(localPath, _bucketName, objectKey, ct);

        _logger.LogInformation("Downloaded {Key} from S3 → {Path}", objectKey, localPath);
        return localPath;
    }

    public async Task<List<CloudBackupObject>> ListAsync(CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(ct);

        var result = new List<CloudBackupObject>();
        var request = new ListObjectsV2Request { BucketName = _bucketName };

        ListObjectsV2Response response;
        do
        {
            response = await _client.ListObjectsV2Async(request, ct);
            result.AddRange(response.S3Objects.Select(o => new CloudBackupObject(
                o.Key,
                Path.GetFileName(o.Key),
                o.Size,
                o.LastModified.ToUniversalTime(),
                o.ETag)));
            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated);

        return result;
    }

    public async Task<bool> DeleteAsync(string objectKey, CancellationToken ct = default)
    {
        await _client.DeleteObjectAsync(_bucketName, objectKey, ct);
        _logger.LogInformation("Deleted {Key} from S3", objectKey);
        return true;
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken ct = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucketName, objectKey, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsureBucketExistsAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "S3 connection test failed");
            return false;
        }
    }

    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        try
        {
            // Check if bucket exists first
            var buckets = await _client.ListBucketsAsync(ct);
            if (buckets.Buckets.Any(b => b.BucketName == _bucketName))
                return;

            await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName }, ct);
            _logger.LogInformation("Created S3 bucket: {Bucket}", _bucketName);
        }
        catch (AmazonS3Exception ex) when (
            ex.ErrorCode == "BucketAlreadyOwnedByYou" ||
            ex.ErrorCode == "BucketAlreadyExists" ||
            ex.ErrorCode == "InvalidBucketName")
        {
            // Already exists or name issue — log and continue
            _logger.LogWarning("Bucket check/create note: {Error}", ex.ErrorCode);
        }
    }

    public void Dispose() => _client.Dispose();
}

/// <summary>
/// HttpClientFactory that accepts self-signed TLS certificates for internal MinIO.
/// </summary>
internal sealed class SelfSignedHttpClientFactory : Amazon.Runtime.HttpClientFactory
{
    public override HttpClient CreateHttpClient(Amazon.Runtime.IClientConfig clientConfig)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        return new HttpClient(handler);
    }
}
