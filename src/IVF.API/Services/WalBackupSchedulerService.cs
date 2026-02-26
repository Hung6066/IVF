using Microsoft.AspNetCore.SignalR;
using IVF.API.Hubs;

namespace IVF.API.Services;

/// <summary>
/// Background service that archives WAL segments every hour.
/// Copies archived WAL files from the PostgreSQL container to the local backups directory,
/// optionally uploading to cloud storage for offsite redundancy.
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

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private const string DbContainer = "ivf-db";

    public WalBackupSchedulerService(
        WalBackupService walService,
        BackupIntegrityService integrityService,
        CloudBackupProviderFactory cloudProviderFactory,
        BackupCompressionService compressionService,
        IHubContext<BackupHub> hubContext,
        IWebHostEnvironment env,
        ILogger<WalBackupSchedulerService> logger)
    {
        _walService = walService;
        _integrityService = integrityService;
        _cloudProviderFactory = cloudProviderFactory;
        _compressionService = compressionService;
        _hubContext = hubContext;
        _env = env;
        _logger = logger;
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

        // Cleanup old WAL files (keep last 7 days)
        CleanupOldWalFiles(walBackupsDir, retentionDays: 7);
    }

    private async Task<int> CopyNewWalFilesAsync(string localDir, CancellationToken ct)
    {
        // List WAL files in the container's archive directory
        var (exit, output) = await RunCommandAsync(
            $"docker exec {DbContainer} sh -c \"ls /var/lib/postgresql/archive/ 2>/dev/null\"", ct);

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
                $"docker cp {DbContainer}:/var/lib/postgresql/archive/{walFile} \"{localPath}\"", ct);

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
                $"docker exec {DbContainer} sh -c \"find /var/lib/postgresql/archive/ -name '0*' -mmin +5 -delete 2>/dev/null\"",
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
