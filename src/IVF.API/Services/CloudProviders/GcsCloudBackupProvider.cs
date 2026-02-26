using Google.Cloud.Storage.V1;
using IVF.Application.Common.Interfaces;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace IVF.API.Services.CloudProviders;

/// <summary>
/// Google Cloud Storage cloud backup provider.
/// </summary>
public sealed class GcsCloudBackupProvider : ICloudBackupProvider
{
    private readonly StorageClient _client;
    private readonly string _bucketName;
    private readonly string? _projectId;
    private readonly ILogger<GcsCloudBackupProvider> _logger;

    public string ProviderName => "Google Cloud Storage";

    public GcsCloudBackupProvider(GcsSettings settings, ILogger<GcsCloudBackupProvider> logger)
    {
        _logger = logger;
        _bucketName = settings.BucketName;
        _projectId = settings.ProjectId;

        _client = !string.IsNullOrEmpty(settings.CredentialsPath)
            ? StorageClient.Create(Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(settings.CredentialsPath))
            : StorageClient.Create();
    }

    public async Task<CloudBackupResult> UploadAsync(string localFilePath, string objectKey, CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(ct);

        await using var stream = File.OpenRead(localFilePath);
        var obj = await _client.UploadObjectAsync(
            _bucketName, objectKey, "application/octet-stream", stream, cancellationToken: ct);

        var size = (long)(obj.Size ?? 0);
        _logger.LogInformation("Uploaded {Key} to GCS ({Size:N0} bytes)", objectKey, size);

        return new CloudBackupResult(objectKey, size, obj.ETag);
    }

    public async Task<string> DownloadAsync(string objectKey, string localDirectory, CancellationToken ct = default)
    {
        var localPath = Path.Combine(localDirectory, Path.GetFileName(objectKey));
        Directory.CreateDirectory(localDirectory);

        await using var outputStream = File.Create(localPath);
        await _client.DownloadObjectAsync(_bucketName, objectKey, outputStream, cancellationToken: ct);

        _logger.LogInformation("Downloaded {Key} from GCS → {Path}", objectKey, localPath);
        return localPath;
    }

    public async Task<List<CloudBackupObject>> ListAsync(CancellationToken ct = default)
    {
        var result = new List<CloudBackupObject>();
        var objects = _client.ListObjectsAsync(_bucketName);

        await foreach (var obj in objects.WithCancellation(ct))
        {
            result.Add(new CloudBackupObject(
                obj.Name,
                Path.GetFileName(obj.Name),
                (long)(obj.Size ?? 0),
                obj.Updated?.ToUniversalTime() ?? DateTime.MinValue,
                obj.ETag));
        }
        return result;
    }

    public async Task<bool> DeleteAsync(string objectKey, CancellationToken ct = default)
    {
        await _client.DeleteObjectAsync(_bucketName, objectKey, cancellationToken: ct);
        _logger.LogInformation("Deleted {Key} from GCS", objectKey);
        return true;
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken ct = default)
    {
        try
        {
            await _client.GetObjectAsync(_bucketName, objectKey, cancellationToken: ct);
            return true;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await _client.GetBucketAsync(_bucketName, cancellationToken: ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GCS connection test failed");
            return false;
        }
    }

    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        try
        {
            await _client.GetBucketAsync(_bucketName, cancellationToken: ct);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (!string.IsNullOrEmpty(_projectId))
                await _client.CreateBucketAsync(_projectId, _bucketName, cancellationToken: ct);
        }
    }
}
