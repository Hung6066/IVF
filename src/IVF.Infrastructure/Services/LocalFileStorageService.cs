using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace IVF.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly string _baseUrl;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _basePath = configuration["FileStorage:BasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _baseUrl = configuration["FileStorage:BaseUrl"] ?? "/api/files";
    }

    public async Task<FileUploadResult> UploadAsync(Stream stream, string fileName, string contentType, string? subfolder = null, CancellationToken ct = default)
    {
        // Build the target directory
        var datePart = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var folder = string.IsNullOrEmpty(subfolder)
            ? Path.Combine(_basePath, datePart)
            : Path.Combine(_basePath, subfolder, datePart);

        Directory.CreateDirectory(folder);

        // Generate unique file name
        var ext = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, uniqueName);

        // Write file
        await using var fileStream = File.Create(fullPath);
        await stream.CopyToAsync(fileStream, ct);
        var fileSize = fileStream.Length;

        // Build relative path for storage
        var relativePath = Path.GetRelativePath(_basePath, fullPath).Replace("\\", "/");
        var url = $"{_baseUrl}/{relativePath}";

        return new FileUploadResult(relativePath, fileName, contentType, fileSize, url);
    }

    public Task<(Stream Stream, string ContentType, string FileName)?> GetAsync(string filePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, filePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (!File.Exists(fullPath))
            return Task.FromResult<(Stream, string, string)?>(null);

        var stream = File.OpenRead(fullPath) as Stream;
        var contentType = GetContentType(Path.GetExtension(fullPath));
        var fileName = Path.GetFileName(fullPath);

        return Task.FromResult<(Stream, string, string)?>((stream, contentType, fileName));
    }

    public Task<bool> DeleteAsync(string filePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, filePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (!File.Exists(fullPath))
            return Task.FromResult(false);

        File.Delete(fullPath);
        return Task.FromResult(true);
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
            _ => "application/octet-stream"
        };
    }
}
