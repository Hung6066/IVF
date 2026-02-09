namespace IVF.Application.Common.Interfaces;

public interface IFileStorageService
{
    /// <summary>
    /// Upload a file and return its stored path/URL
    /// </summary>
    Task<FileUploadResult> UploadAsync(Stream stream, string fileName, string contentType, string? subfolder = null, CancellationToken ct = default);

    /// <summary>
    /// Get a file stream by its stored path
    /// </summary>
    Task<(Stream Stream, string ContentType, string FileName)?> GetAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Delete a file by its stored path
    /// </summary>
    Task<bool> DeleteAsync(string filePath, CancellationToken ct = default);
}

public record FileUploadResult(
    string FilePath,
    string FileName,
    string ContentType,
    long FileSize,
    string Url
);
