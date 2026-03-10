using Microsoft.AspNetCore.SignalR;
using IVF.API.Hubs;
using IVF.Application.Common.Interfaces;

namespace IVF.API.Services;

/// <summary>
/// Background service that archives WAL segments every hour.
/// Copies archived WAL files from the PostgreSQL container to the local backups directory,
/// optionally uploading to cloud storage for offsite redundancy.
/// Uses distributed lock to prevent concurrent execution across replicas.
/// </summary>
public sealed class WalBackupSchedulerService : BackgroundService
{
    private readonly WalBackupService _walService;
    private readonly BackupIntegrityService _integrityService;
    private readonly CloudBackupProviderFactory _cloudProviderFactory;
    private readonly BackupCompressionService _compressionService;
    private readonly IHubContext<BackupHub> _hubContext;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<WalBackupSchedulerService> _logger;
    private readonly IDistributedLockService _lockService;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public WalBackupSchedulerService(
        WalBackupService walService,
        BackupIntegrityService integrityService,
        CloudBackupProviderFactory cloudProviderFactory,
        BackupCompressionService compressionService,
        IHubContext<BackupHub> hubContext,
        IWebHostEnvironment env,
        ILogger<WalBackupSchedulerService> logger,
        IDistributedLockService lockService)
    {
        _walService = walService;
        _integrityService = integrityService;
        _cloudProviderFactory = cloudProviderFactory;
        _compressionService = compressionService;
        _hubContext = hubContext;
        _env = env;
        _logger = logger;
        _lockService = lockService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        _logger.LogInformation("WAL backup scheduler started — interval: {Interval}", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Acquire distributed lock to prevent concurrent WAL backup across replicas
                await using var lockHandle = await _lockService.TryAcquireAsync("lock:wal-backup", TimeSpan.FromMinutes(10), stoppingToken);
                if (lockHandle is null)
                {
                    _logger.LogDebug("Another replica is running WAL backup — skipping");
                    await Task.Delay(Interval, stoppingToken);
                    continue;
                }

                await ArchiveWalSegmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WAL backup scheduler error");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ArchiveWalSegmentsAsync(CancellationToken ct)
    {
        // Check if WAL archiving is enabled
        var status = await _walService.GetWalStatusAsync(ct);
        if (!status.IsArchivingEnabled)
        {
            _logger.LogDebug("WAL archiving not enabled — skipping cycle");
            return;
        }

        // Force a WAL segment switch to flush current segment to archive
        var (switched, switchMsg) = await _walService.SwitchWalAsync(ct);
        if (!switched)
        {
            _logger.LogWarning("WAL switch failed: {Message}", switchMsg);
            return;
        }

        // Small delay for archive_command to copy the segment
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        // Copy archived WAL files from container to local backups
        var walBackupsDir = Path.Combine(GetBackupsDir(), "wal");
        Directory.CreateDirectory(walBackupsDir);

        var copied = await CopyNewWalFilesAsync(walBackupsDir, ct);
        if (copied == 0)
        {
            _logger.LogDebug("No new WAL segments to archive");
            return;
        }

        _logger.LogInformation("Archived {Count} WAL segment(s) to {Dir}", copied, walBackupsDir);
        await _hubContext.Clients.All.SendAsync("backupLog",
            "wal_backup", "OK", $"Archived {copied} WAL segment(s)", ct);

        // Upload to cloud if configured
        await UploadWalToCloudAsync(walBackupsDir, ct);

        // Cleanup old WAL files (keep last 14 days — 2-week PITR window)
        CleanupOldWalFiles(walBackupsDir, retentionDays: 14);
    }

    private async Task<int> CopyNewWalFilesAsync(string localDir, CancellationToken ct)
    {
        var dbContainer = await ResolveDbContainerAsync(ct);
        if (dbContainer == null)
        {
            _logger.LogDebug("PostgreSQL container not found on this node — skipping WAL copy");
            return 0;
        }

        // List WAL files in the container's archive directory
        var (exit, output) = await RunCommandAsync(
            $"docker exec {dbContainer} sh -c \"ls /var/lib/postgresql/archive/ 2>/dev/null\"", ct);

        if (exit != 0 || string.IsNullOrWhiteSpace(output))
            return 0;

        var walFiles = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(f => !string.IsNullOrWhiteSpace(f) && !f.Contains("No such file"))
            .ToList();

        int copied = 0;
        foreach (var walFile in walFiles)
        {
            var localPath = Path.Combine(localDir, walFile);
            if (File.Exists(localPath))
                continue; // Already archived

            var (cpExit, _) = await RunCommandAsync(
                $"docker cp {dbContainer}:/var/lib/postgresql/archive/{walFile} \"{localPath}\"", ct);

            if (cpExit == 0)
            {
                await _integrityService.ComputeAndStoreChecksumAsync(localPath, ct);
                copied++;
            }
        }

        // Purge archived files from container to save space
        if (copied > 0)
        {
            await RunCommandAsync(
                $"docker exec {dbContainer} sh -c \"find /var/lib/postgresql/archive/ -name '0*' -mmin +5 -delete 2>/dev/null\"",
                ct);
        }

        return copied;
    }

    private async Task UploadWalToCloudAsync(string walDir, CancellationToken ct)
    {
        try
        {
            var cloudProvider = await _cloudProviderFactory.GetProviderAsync(ct);

            var walFiles = Directory.GetFiles(walDir)
                .Where(f => !f.EndsWith(".sha256") && !f.EndsWith(".br"))
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTimeUtc > DateTime.UtcNow.AddHours(-2)) // Only recent files
                .ToList();

            var cloudConfig = await _cloudProviderFactory.GetConfigAsync(ct);

            foreach (var file in walFiles)
            {
                var objectKey = $"wal-archives/{file.Name}";

                if (cloudConfig.CompressionEnabled)
                {
                    var compressed = await _compressionService.CompressAsync(file.FullName, ct: ct);
                    try
                    {
                        await cloudProvider.UploadAsync(
                            compressed.CompressedFilePath,
                            objectKey + BackupCompressionService.CompressedExtension,
                            ct);
                    }
                    finally
                    {
                        try { File.Delete(compressed.CompressedFilePath); } catch { }
                    }
                }
                else
                {
                    await cloudProvider.UploadAsync(file.FullName, objectKey, ct);
                }
            }

            if (walFiles.Count > 0)
                _logger.LogInformation("Uploaded {Count} WAL file(s) to cloud", walFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upload WAL files to cloud (non-fatal)");
        }
    }

    /// <summary>
    /// On-demand WAL archive + cloud upload. Called from endpoint for manual trigger.
    /// </summary>
    public async Task<WalBackupToS3Result> RunOnDemandArchiveAsync(string walBackupsDir, CancellationToken ct)
    {
        var copied = await CopyNewWalFilesAsync(walBackupsDir, ct);
        int uploaded = 0;

        if (copied > 0)
        {
            uploaded = await CountAndUploadToCloudAsync(walBackupsDir, ct);
            _logger.LogInformation("On-demand WAL backup: copied {Copied}, uploaded {Uploaded}", copied, uploaded);
        }

        return new WalBackupToS3Result(
            copied,
            uploaded,
            copied == 0 ? "No new WAL segments to archive" : $"Archived {copied} segment(s), uploaded {uploaded} to S3");
    }

    private async Task<int> CountAndUploadToCloudAsync(string walDir, CancellationToken ct)
    {
        try
        {
            var cloudProvider = await _cloudProviderFactory.GetProviderAsync(ct);
            var walFiles = Directory.GetFiles(walDir)
                .Where(f => !f.EndsWith(".sha256") && !f.EndsWith(".br"))
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTimeUtc > DateTime.UtcNow.AddHours(-2))
                .ToList();

            var cloudConfig = await _cloudProviderFactory.GetConfigAsync(ct);
            int uploaded = 0;

            foreach (var file in walFiles)
            {
                var objectKey = $"wal-archives/{file.Name}";
                if (cloudConfig.CompressionEnabled)
                {
                    var compressed = await _compressionService.CompressAsync(file.FullName, ct: ct);
                    try
                    {
                        await cloudProvider.UploadAsync(
                            compressed.CompressedFilePath,
                            objectKey + BackupCompressionService.CompressedExtension, ct);
                        uploaded++;
                    }
                    finally
                    {
                        if (File.Exists(compressed.CompressedFilePath))
                            File.Delete(compressed.CompressedFilePath);
                    }
                }
                else
                {
                    await cloudProvider.UploadAsync(file.FullName, objectKey, ct);
                    uploaded++;
                }
            }

            return uploaded;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "On-demand WAL S3 upload failed (non-fatal)");
            return 0;
        }
    }

    private void CleanupOldWalFiles(string walDir, int retentionDays)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            var oldFiles = Directory.GetFiles(walDir)
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTimeUtc < cutoff)
                .ToList();

            foreach (var file in oldFiles)
            {
                file.Delete();
                // Also delete checksum file
                var checksumFile = file.FullName + ".sha256";
                if (File.Exists(checksumFile))
                    File.Delete(checksumFile);
            }

            if (oldFiles.Count > 0)
                _logger.LogInformation("Cleaned up {Count} old WAL file(s) (>{Days}d)", oldFiles.Count, retentionDays);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WAL cleanup failed");
        }
    }

    private string GetBackupsDir() => Path.Combine(
        Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "..")), "backups");

    private static async Task<string?> ResolveDbContainerAsync(CancellationToken ct)
    {
        var (exit, output) = await RunCommandAsync(
            "docker ps -q -f name=ivf_db.1 --no-trunc", ct);
        if (exit == 0 && !string.IsNullOrWhiteSpace(output))
            return output.Trim().Split('\n')[0].Trim();

        var (exit2, output2) = await RunCommandAsync(
            "docker ps -q -f name=ivf-db --no-trunc", ct);
        if (exit2 == 0 && !string.IsNullOrWhiteSpace(output2))
            return output2.Trim().Split('\n')[0].Trim();

        return null;
    }

    private static async Task<(int ExitCode, string Output)> RunCommandAsync(string command, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start process");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        var stdout = stdoutTask.Result;
        var stderr = stderrTask.Result;
        return (process.ExitCode, string.IsNullOrWhiteSpace(stdout) ? stderr : stdout);
    }
}

public record WalBackupToS3Result(
    int SegmentsCopied,
    int SegmentsUploaded,
    string Message);
