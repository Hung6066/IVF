using System.Diagnostics;
using IVF.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace IVF.API.Services;

/// <summary>
/// Service for backing up and restoring MinIO object storage data.
/// Uses the MinIO Client (mc) CLI tool via Docker exec on the ivf-minio container.
/// </summary>
public sealed class MinioBackupService(
    IOptions<MinioOptions> minioOptions,
    BackupIntegrityService integrityService,
    ILogger<MinioBackupService> logger)
{
    private const string MinioContainer = "ivf-minio";
    private readonly MinioOptions _options = minioOptions.Value;

    /// <summary>
    /// Backup all MinIO buckets by mirroring them to a local tar.gz archive.
    /// Creates a tar.gz of all objects across configured buckets.
    /// </summary>
    public async Task<(string FilePath, long SizeBytes)> BackupMinioAsync(
        string outputDir,
        Action<string, string>? onLog = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"ivf_minio_{timestamp}.tar.gz";
        var localPath = Path.Combine(outputDir, backupFileName);

        var buckets = new[] { _options.DocumentsBucket, _options.SignedPdfsBucket, _options.MedicalImagesBucket };

        try
        {
            onLog?.Invoke("INFO", "Starting MinIO backup...");

            // Create a temp directory for staging
            var tempDir = Path.Combine(Path.GetTempPath(), $"minio_backup_{timestamp}");
            Directory.CreateDirectory(tempDir);

            try
            {
                long totalObjects = 0;

                foreach (var bucket in buckets)
                {
                    onLog?.Invoke("INFO", $"Backing up bucket: {bucket}...");

                    var bucketDir = Path.Combine(tempDir, bucket);
                    Directory.CreateDirectory(bucketDir);

                    // Use docker cp to export the bucket data
                    // MinIO stores data at /data/{bucket}/ inside the container
                    var checkCmd = $"docker exec {MinioContainer} sh -c \"test -d /data/{bucket} && echo exists || echo empty\"";
                    var (checkExit, checkOutput) = await RunCommandAsync(checkCmd, ct);

                    if (checkOutput.Trim() != "exists")
                    {
                        onLog?.Invoke("WARN", $"Bucket '{bucket}' is empty or not found, skipping");
                        continue;
                    }

                    // Copy bucket data from container
                    var cpCmd = $"docker cp {MinioContainer}:/data/{bucket}/. \"{bucketDir}\"";
                    var (cpExit, cpOutput) = await RunCommandAsync(cpCmd, ct);

                    if (cpExit != 0)
                    {
                        onLog?.Invoke("WARN", $"Failed to copy bucket '{bucket}': {cpOutput}");
                        continue;
                    }

                    // Count objects
                    var files = Directory.GetFiles(bucketDir, "*", SearchOption.AllDirectories);
                    totalObjects += files.Length;
                    var bucketSize = files.Sum(f => new FileInfo(f).Length);
                    onLog?.Invoke("OK", $"Bucket '{bucket}': {files.Length} objects ({bucketSize:N0} bytes)");
                }

                if (totalObjects == 0)
                {
                    onLog?.Invoke("WARN", "No objects found in any bucket");
                }

                // Create tar.gz archive of all bucket data
                onLog?.Invoke("INFO", "Creating archive...");
                await CreateTarGzAsync(tempDir, localPath, ct);

                if (!File.Exists(localPath))
                    throw new FileNotFoundException("MinIO backup archive was not created");

                var size = new FileInfo(localPath).Length;

                // Compute and store SHA-256 checksum
                var checksum = await integrityService.ComputeAndStoreChecksumAsync(localPath, ct);
                onLog?.Invoke("OK", $"SHA-256: {checksum}");

                // Verify archive integrity
                var (testExit, testOutput) = await RunCommandAsync($"tar -tzf \"{localPath}\" > nul 2>&1", ct);
                if (testExit != 0)
                    throw new InvalidOperationException($"Archive integrity check failed: {testOutput}");
                onLog?.Invoke("OK", "Archive integrity verified");

                onLog?.Invoke("OK", $"MinIO backup saved: {backupFileName} ({size:N0} bytes, {totalObjects} objects)"); ;

                return (localPath, size);
            }
            finally
            {
                // Cleanup temp directory
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
                catch { /* ignore cleanup errors */ }
            }
        }
        catch (Exception ex)
        {
            onLog?.Invoke("ERROR", $"MinIO backup failed: {ex.Message}");
            logger.LogError(ex, "MinIO backup failed");
            throw;
        }
    }

    /// <summary>
    /// Restore MinIO data from a tar.gz backup archive.
    /// </summary>
    public async Task RestoreMinioAsync(
        string backupFilePath,
        Action<string, string>? onLog = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException($"MinIO backup file not found: {backupFilePath}");

        var buckets = new[] { _options.DocumentsBucket, _options.SignedPdfsBucket, _options.MedicalImagesBucket };

        try
        {
            onLog?.Invoke("INFO", $"Starting MinIO restore from '{Path.GetFileName(backupFilePath)}'...");

            // Verify checksum before starting restore
            var checksumResult = await integrityService.VerifyChecksumAsync(backupFilePath, ct);
            if (checksumResult.ExpectedChecksum != null)
            {
                if (!checksumResult.IsValid)
                    throw new InvalidOperationException($"Checksum verification failed: {checksumResult.Error}");
                onLog?.Invoke("OK", $"Checksum verified: {checksumResult.ActualChecksum}");
            }
            else
            {
                onLog?.Invoke("WARN", "No checksum file found — skipping integrity verification");
            }

            // Validate archive integrity before extracting
            onLog?.Invoke("INFO", "Validating archive integrity...");
            var (testExit, testOutput) = await RunCommandAsync($"tar -tzf \"{backupFilePath}\" > nul 2>&1", ct);
            if (testExit != 0)
                throw new InvalidOperationException($"Archive integrity check failed — refusing to restore corrupted backup");
            onLog?.Invoke("OK", "Archive integrity verified");

            // Extract tar.gz to temp directory
            var tempDir = Path.Combine(Path.GetTempPath(), $"minio_restore_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(tempDir);

            try
            {
                await ExtractTarGzAsync(backupFilePath, tempDir, ct);
                onLog?.Invoke("OK", "Archive extracted");

                foreach (var bucket in buckets)
                {
                    var bucketDir = Path.Combine(tempDir, bucket);
                    if (!Directory.Exists(bucketDir))
                    {
                        onLog?.Invoke("WARN", $"Bucket '{bucket}' not found in backup, skipping");
                        continue;
                    }

                    var files = Directory.GetFiles(bucketDir, "*", SearchOption.AllDirectories);
                    onLog?.Invoke("INFO", $"Restoring bucket '{bucket}' ({files.Length} objects)...");

                    // Ensure bucket directory exists in container
                    var mkdirCmd = $"docker exec {MinioContainer} mkdir -p /data/{bucket}";
                    await RunCommandAsync(mkdirCmd, ct);

                    // Copy bucket data back to container
                    var cpCmd = $"docker cp \"{bucketDir}/.\" {MinioContainer}:/data/{bucket}/";
                    var (cpExit, cpOutput) = await RunCommandAsync(cpCmd, ct);

                    if (cpExit != 0)
                    {
                        onLog?.Invoke("ERROR", $"Failed to restore bucket '{bucket}': {cpOutput}");
                        continue;
                    }

                    onLog?.Invoke("OK", $"Bucket '{bucket}' restored ({files.Length} objects)");
                }

                onLog?.Invoke("OK", "MinIO restore completed");
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
                catch { /* ignore cleanup errors */ }
            }
        }
        catch (Exception ex)
        {
            onLog?.Invoke("ERROR", $"MinIO restore failed: {ex.Message}");
            logger.LogError(ex, "MinIO restore failed");
            throw;
        }
    }

    /// <summary>
    /// Get MinIO storage info for status reporting.
    /// </summary>
    public async Task<MinioStorageInfo> GetMinioInfoAsync(CancellationToken ct = default)
    {
        var buckets = new[] { _options.DocumentsBucket, _options.SignedPdfsBucket, _options.MedicalImagesBucket };
        var bucketInfos = new List<BucketInfo>();

        try
        {
            foreach (var bucket in buckets)
            {
                var countCmd = $"docker exec {MinioContainer} sh -c \"find /data/{bucket} -type f 2>/dev/null | wc -l\"";
                var (countExit, countOutput) = await RunCommandAsync(countCmd, ct);
                int objectCount = 0;
                if (countExit == 0) int.TryParse(countOutput.Trim(), out objectCount);

                var sizeCmd = $"docker exec {MinioContainer} sh -c \"du -sb /data/{bucket} 2>/dev/null | cut -f1\"";
                var (sizeExit, sizeOutput) = await RunCommandAsync(sizeCmd, ct);
                long sizeBytes = 0;
                if (sizeExit == 0) long.TryParse(sizeOutput.Trim(), out sizeBytes);

                bucketInfos.Add(new BucketInfo(bucket, objectCount, sizeBytes));
            }

            return new MinioStorageInfo(
                bucketInfos,
                bucketInfos.Sum(b => b.ObjectCount),
                bucketInfos.Sum(b => b.SizeBytes),
                true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get MinIO info");
            return new MinioStorageInfo([], 0, 0, false);
        }
    }

    /// <summary>
    /// List existing MinIO backup files.
    /// </summary>
    public List<BackupInfo> ListMinioBackups(string backupsDir)
    {
        if (!Directory.Exists(backupsDir))
            return [];

        return Directory.GetFiles(backupsDir, "ivf_minio_*.tar.gz")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new BackupInfo
            {
                FileName = f.Name,
                FullPath = f.FullName,
                SizeBytes = f.Length,
                CreatedAt = f.LastWriteTimeUtc,
                Checksum = BackupIntegrityService.LoadStoredChecksum(f.FullName)
            })
            .ToList();
    }

    /// <summary>
    /// Validate a MinIO backup archive without restoring.
    /// </summary>
    public async Task<MinioBackupValidationResult> ValidateBackupAsync(
        string backupFilePath,
        Action<string, string>? onLog = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath))
            return new MinioBackupValidationResult(false, "File not found", null, 0, 0);

        var size = new FileInfo(backupFilePath).Length;
        if (size < 50)
            return new MinioBackupValidationResult(false, "File is too small to be a valid archive", null, 0, 0);

        // Checksum verification
        ChecksumResult? checksumResult = null;
        var checksumPath = backupFilePath + ".sha256";
        if (File.Exists(checksumPath))
        {
            checksumResult = await integrityService.VerifyChecksumAsync(backupFilePath, ct);
            if (!checksumResult.IsValid)
                return new MinioBackupValidationResult(false, $"Checksum mismatch: {checksumResult.Error}", checksumResult.ActualChecksum, 0, 0);
            onLog?.Invoke("OK", $"Checksum verified: {checksumResult.ActualChecksum}");
        }

        // Archive integrity test
        var (testExit, _) = await RunCommandAsync($"tar -tzf \"{backupFilePath}\" > nul 2>&1", ct);
        if (testExit != 0)
            return new MinioBackupValidationResult(false, "Archive integrity check failed (tar.gz corrupted)", checksumResult?.ActualChecksum, 0, 0);

        // Count entries
        var (listExit, listOutput) = await RunCommandAsync($"tar -tzf \"{backupFilePath}\"", ct);
        var entries = listOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var bucketCount = entries.Select(e => e.Split('/').FirstOrDefault()).Where(b => !string.IsNullOrEmpty(b) && b != ".").Distinct().Count();

        return new MinioBackupValidationResult(true, null, checksumResult?.ActualChecksum, bucketCount, entries.Length);
    }

    private static async Task CreateTarGzAsync(string sourceDir, string outputPath, CancellationToken ct)
    {
        var cmd = OperatingSystem.IsWindows()
            ? $"tar -czf \"{outputPath}\" -C \"{sourceDir}\" ."
            : $"tar -czf \"{outputPath}\" -C \"{sourceDir}\" .";

        var (exitCode, output) = await RunCommandAsync(cmd, ct);
        if (exitCode != 0)
            throw new InvalidOperationException($"tar create failed: {output}");
    }

    private static async Task ExtractTarGzAsync(string archivePath, string outputDir, CancellationToken ct)
    {
        var cmd = $"tar -xzf \"{archivePath}\" -C \"{outputDir}\"";
        var (exitCode, output) = await RunCommandAsync(cmd, ct);
        if (exitCode != 0)
            throw new InvalidOperationException($"tar extract failed: {output}");
    }

    private static async Task<(int ExitCode, string Output)> RunCommandAsync(string command, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");

        // Read stdout and stderr concurrently to avoid pipe deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        var stdout = stdoutTask.Result;
        var stderr = stderrTask.Result;
        var output = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
        return (process.ExitCode, output);
    }
}

public record MinioStorageInfo(
    List<BucketInfo> Buckets,
    int TotalObjects,
    long TotalSizeBytes,
    bool Connected);

public record BucketInfo(string Name, int ObjectCount, long SizeBytes);

public record MinioBackupValidationResult(bool IsValid, string? Error, string? Checksum, int BucketCount, int EntryCount);
