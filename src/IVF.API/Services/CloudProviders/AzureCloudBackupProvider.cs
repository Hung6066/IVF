using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using IVF.Application.Common.Interfaces;

namespace IVF.API.Services.CloudProviders;

/// <summary>
/// Azure Blob Storage cloud backup provider.
/// </summary>
public sealed class AzureCloudBackupProvider : ICloudBackupProvider
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<AzureCloudBackupProvider> _logger;

    public string ProviderName => "Azure Blob Storage";

    public AzureCloudBackupProvider(AzureBlobSettings settings, ILogger<AzureCloudBackupProvider> logger)
    {
        _logger = logger;

        if (string.IsNullOrEmpty(settings.ConnectionString))
            throw new ArgumentException("Azure Blob Storage ConnectionString is required");

        var serviceClient = new BlobServiceClient(settings.ConnectionString);
        _container = serviceClient.GetBlobContainerClient(settings.ContainerName);
    }

    public async Task<CloudBackupResult> UploadAsync(string localFilePath, string objectKey, CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);

        var blob = _container.GetBlobClient(objectKey);
        await using var stream = File.OpenRead(localFilePath);
        await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);

        var props = await blob.GetPropertiesAsync(cancellationToken: ct);
        _logger.LogInformation("Uploaded {Key} to Azure Blob ({Size:N0} bytes)", objectKey, props.Value.ContentLength);

        return new CloudBackupResult(objectKey, props.Value.ContentLength, props.Value.ETag.ToString());
    }

    public async Task<string> DownloadAsync(string objectKey, string localDirectory, CancellationToken ct = default)
    {
        var localPath = Path.Combine(localDirectory, Path.GetFileName(objectKey));
        Directory.CreateDirectory(localDirectory);

        var blob = _container.GetBlobClient(objectKey);
        await blob.DownloadToAsync(localPath, ct);

        _logger.LogInformation("Downloaded {Key} from Azure Blob → {Path}", objectKey, localPath);
        return localPath;
    }

    public async Task<List<CloudBackupObject>> ListAsync(CancellationToken ct = default)
    {
        var result = new List<CloudBackupObject>();
        await foreach (var item in _container.GetBlobsAsync(cancellationToken: ct))
        {
            result.Add(new CloudBackupObject(
                item.Name,
                Path.GetFileName(item.Name),
                item.Properties.ContentLength ?? 0,
                item.Properties.LastModified?.UtcDateTime ?? DateTime.MinValue,
                item.Properties.ETag?.ToString()));
        }
        return result;
    }

    public async Task<bool> DeleteAsync(string objectKey, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(objectKey);
        var response = await blob.DeleteIfExistsAsync(cancellationToken: ct);
        _logger.LogInformation("Deleted {Key} from Azure Blob", objectKey);
        return response.Value;
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(objectKey);
        var response = await blob.ExistsAsync(ct);
        return response.Value;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await _container.CreateIfNotExistsAsync(cancellationToken: ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Blob connection test failed");
            return false;
        }
    }
}
