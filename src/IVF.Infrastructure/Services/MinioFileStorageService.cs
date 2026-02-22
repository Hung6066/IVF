using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IVF.Infrastructure.Services;

/// <summary>
/// MinIO-backed IFileStorageService implementation
/// Drop-in replacement for LocalFileStorageService - used by FormEndpoints file upload/download
/// </summary>
public class MinioFileStorageService : IFileStorageService
{
    private readonly IObjectStorageService _objectStorage;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioFileStorageService> _logger;

    public MinioFileStorageService(
        IObjectStorageService objectStorage,
        IOptions<MinioOptions> options,
        ILogger<MinioFileStorageService> logger)
    {
        _objectStorage = objectStorage;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FileUploadResult> UploadAsync(
        Stream stream, string fileName, string contentType,
        string? subfolder = null, CancellationToken ct = default)
    {
        var bucket = _options.DocumentsBucket;
        var datePart = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var ext = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}{ext}";

        var objectKey = string.IsNullOrEmpty(subfolder)
            ? $"{datePart}/{uniqueName}"
            : $"{subfolder}/{datePart}/{uniqueName}";

        // Calculate size
        long size = stream.Length;
        if (size == 0 && stream.CanSeek)
        {
            stream.Position = 0;
            size = stream.Length;
        }

        var result = await _objectStorage.UploadAsync(
            bucket, objectKey, stream, contentType, size, ct: ct);

        var url = $"/api/files/{objectKey}";

        _logger.LogInformation(
            "File uploaded to MinIO: {ObjectKey} ({Size} bytes)", objectKey, size);

        return new FileUploadResult(objectKey, fileName, contentType, size, url);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> GetAsync(
        string filePath, CancellationToken ct = default)
    {
        var bucket = _options.DocumentsBucket;
        var stream = await _objectStorage.DownloadAsync(bucket, filePath, ct);

        if (stream == null)
            return null;

        var contentType = GetContentType(Path.GetExtension(filePath));
        var fileName = Path.GetFileName(filePath);

        return (stream, contentType, fileName);
    }

    public async Task<bool> DeleteAsync(string filePath, CancellationToken ct = default)
    {
        var bucket = _options.DocumentsBucket;
        return await _objectStorage.DeleteAsync(bucket, filePath, ct);
    }

    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            ".dicom" or ".dcm" => "application/dicom",
            _ => "application/octet-stream"
        };
    }
}
