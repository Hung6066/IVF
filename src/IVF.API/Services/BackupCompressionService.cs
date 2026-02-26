using System.IO.Compression;

namespace IVF.API.Services;

/// <summary>
/// Handles Brotli compression/decompression for backup files before cloud upload.
/// Brotli provides better compression ratio than gzip for already-compressed archives,
/// optimizing cloud storage capacity and download speed.
/// </summary>
public sealed class BackupCompressionService(ILogger<BackupCompressionService> logger)
{
    public const string CompressedExtension = ".br";

    /// <summary>
    /// Compress a file using Brotli, returning the path to the compressed file and stats.
    /// </summary>
    public async Task<CompressionResult> CompressAsync(
        string sourceFilePath,
        CompressionLevel level = CompressionLevel.Optimal,
        CancellationToken ct = default)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source file not found", sourceFilePath);

        var compressedPath = sourceFilePath + CompressedExtension;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var originalSize = new FileInfo(sourceFilePath).Length;

        logger.LogInformation("Compressing {File} ({Size:N0} bytes) with Brotli level={Level}",
            Path.GetFileName(sourceFilePath), originalSize, level);

        await using (var sourceStream = File.OpenRead(sourceFilePath))
        await using (var outputStream = File.Create(compressedPath))
        await using (var brotliStream = new BrotliStream(outputStream, level))
        {
            await sourceStream.CopyToAsync(brotliStream, ct);
        }

        sw.Stop();
        var compressedSize = new FileInfo(compressedPath).Length;
        var ratio = originalSize > 0 ? (double)compressedSize / originalSize * 100 : 0;

        logger.LogInformation("Compressed {File}: {Original:N0} → {Compressed:N0} bytes ({Ratio:F1}%) in {Duration}ms",
            Path.GetFileName(sourceFilePath), originalSize, compressedSize, ratio, sw.ElapsedMilliseconds);

        return new CompressionResult(
            compressedPath,
            originalSize,
            compressedSize,
            ratio,
            sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Decompress a Brotli-compressed file, returning the path to the decompressed file.
    /// </summary>
    public async Task<string> DecompressAsync(
        string compressedFilePath,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(compressedFilePath))
            throw new FileNotFoundException("Compressed file not found", compressedFilePath);

        outputPath ??= compressedFilePath.EndsWith(CompressedExtension, StringComparison.OrdinalIgnoreCase)
            ? compressedFilePath[..^CompressedExtension.Length]
            : compressedFilePath + ".decompressed";

        logger.LogInformation("Decompressing {File} → {Output}",
            Path.GetFileName(compressedFilePath), Path.GetFileName(outputPath));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        await using (var compressedStream = File.OpenRead(compressedFilePath))
        await using (var brotliStream = new BrotliStream(compressedStream, CompressionMode.Decompress))
        await using (var outputStream = File.Create(outputPath))
        {
            await brotliStream.CopyToAsync(outputStream, ct);
        }

        sw.Stop();
        logger.LogInformation("Decompressed in {Duration}ms → {Size:N0} bytes",
            sw.ElapsedMilliseconds, new FileInfo(outputPath).Length);

        return outputPath;
    }
}

public record CompressionResult(
    string CompressedFilePath,
    long OriginalSizeBytes,
    long CompressedSizeBytes,
    double CompressionRatioPercent,
    long DurationMs);
