using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using IVF.API.Hubs;
using IVF.Application.Common.Interfaces;
using IVF.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Services;

// In-memory DTO for real-time streaming (not persisted directly)
public sealed class BackupLogLine
{
    public DateTime Timestamp { get; init; }
    public string Level { get; init; } = "INFO";
    public string Message { get; init; } = "";
}

public sealed class BackupInfo
{
    public string FileName { get; init; } = "";
    public string FullPath { get; init; } = "";
    public long SizeBytes { get; init; }
    public DateTime CreatedAt { get; init; }
}

// In-memory operation tracker for live streaming
internal sealed class LiveOperation
{
    public string OperationCode { get; init; } = null!;
    public Guid EntityId { get; init; }
    public List<BackupLogLine> LogLines { get; } = [];
}

public sealed class BackupRestoreService(
    IHubContext<BackupHub> hubContext,
    ILogger<BackupRestoreService> logger,
    IWebHostEnvironment env,
    IServiceScopeFactory scopeFactory,
    CloudBackupProviderFactory cloudProviderFactory,
    BackupCompressionService compressionService)
{
    // In-memory: only for live log streaming during running operations
    private readonly ConcurrentDictionary<string, LiveOperation> _liveOps = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();

    private string ProjectDir => Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", ".."));
    private string ScriptsDir => Path.Combine(ProjectDir, "scripts");
    private string BackupsDir => Path.Combine(ProjectDir, "backups");

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public IReadOnlyList<BackupInfo> ListBackups()
    {
        if (!Directory.Exists(BackupsDir))
            return [];

        return Directory.GetFiles(BackupsDir, "ivf-ca-backup_*.tar.gz")
            .Where(f => !f.EndsWith(".enc"))
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new BackupInfo
            {
                FileName = f.Name,
                FullPath = f.FullName,
                SizeBytes = f.Length,
                CreatedAt = f.LastWriteTimeUtc
            })
            .ToList();
    }

    // ─── Read from DB ───────────────────────────────────────

    public async Task<List<IVF.Domain.Entities.BackupOperation>> GetOperationHistoryAsync(int limit = 50)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        return await db.BackupOperations
            .OrderByDescending(o => o.StartedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IVF.Domain.Entities.BackupOperation?> GetOperationByCodeAsync(string operationCode)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        return await db.BackupOperations
            .FirstOrDefaultAsync(o => o.OperationCode == operationCode);
    }

    /// <summary>
    /// Get live log lines for a running operation, or stored logs from DB for completed ones.
    /// </summary>
    public async Task<List<BackupLogLine>> GetLogsAsync(string operationCode)
    {
        // If live, return from memory
        if (_liveOps.TryGetValue(operationCode, out var live))
            return [.. live.LogLines];

        // Otherwise, load from DB
        var op = await GetOperationByCodeAsync(operationCode);
        if (op?.LogLinesJson == null) return [];

        return JsonSerializer.Deserialize<List<BackupLogLine>>(op.LogLinesJson, JsonOpts) ?? [];
    }

    // ─── Start operations ───────────────────────────────────

    public async Task<string> StartBackupAsync(bool keysOnly, string? startedBy)
    {
        var operationCode = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";

        var entity = IVF.Domain.Entities.BackupOperation.Create(
            operationCode,
            IVF.Domain.Entities.BackupOperationType.Backup,
            startedBy);

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            db.BackupOperations.Add(entity);
            await db.SaveChangesAsync();
        }

        var live = new LiveOperation { OperationCode = operationCode, EntityId = entity.Id };
        _liveOps[operationCode] = live;

        var cts = new CancellationTokenSource();
        _cancellations[operationCode] = cts;

        var scriptArgs = keysOnly ? "--keys-only" : "";
        _ = RunScriptAsync(live, "backup-ca-keys.sh", scriptArgs, cts.Token);

        return operationCode;
    }

    public async Task<string> StartRestoreAsync(string archiveFileName, bool keysOnly, bool dryRun, string? startedBy)
    {
        var archivePath = Path.Combine(BackupsDir, archiveFileName);
        if (!File.Exists(archivePath))
            throw new FileNotFoundException($"Backup archive not found: {archiveFileName}");

        var prefix = dryRun ? "dryrun" : "restore";
        var operationCode = $"{prefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";

        var entity = IVF.Domain.Entities.BackupOperation.Create(
            operationCode,
            IVF.Domain.Entities.BackupOperationType.Restore,
            startedBy);
        entity.SetArchivePath(archiveFileName);

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            db.BackupOperations.Add(entity);
            await db.SaveChangesAsync();
        }

        var live = new LiveOperation { OperationCode = operationCode, EntityId = entity.Id };
        _liveOps[operationCode] = live;

        var cts = new CancellationTokenSource();
        _cancellations[operationCode] = cts;

        var args = "--yes";
        if (keysOnly) args += " --keys-only";
        if (dryRun) args += " --dry-run";
        args += $" backups/{archiveFileName}";

        _ = RunScriptAsync(live, "restore-ca-keys.sh", args, cts.Token);

        return operationCode;
    }

    public bool CancelOperation(string operationCode)
    {
        if (_cancellations.TryGetValue(operationCode, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    public bool IsRunning(string operationCode) => _liveOps.ContainsKey(operationCode);

    // ─── Script execution ───────────────────────────────────

    private async Task RunScriptAsync(LiveOperation live, string scriptName, string args, CancellationToken ct)
    {
        var scriptPath = Path.Combine(ScriptsDir, scriptName);

        var bashPath = FindBash();
        if (bashPath == null)
        {
            await AppendLog(live, "ERROR", "Cannot find bash. Install Git for Windows or WSL.");
            await PersistFinalState(live, IVF.Domain.Entities.BackupOperationStatus.Failed, "Bash not found");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = bashPath,
            Arguments = $"\"{scriptPath}\" {args}",
            WorkingDirectory = ProjectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["MSYS_NO_PATHCONV"] = "1";

        try
        {
            await AppendLog(live, "INFO", $"Starting {scriptName} {args}");
            await NotifyStatusChanged(live, "Running");

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start process");

            var outputTask = ReadStreamAsync(process.StandardOutput, live, ct);
            var errorTask = ReadStreamAsync(process.StandardError, live, ct);

            ct.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch { /* ignore */ }
            });

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync(ct);

            if (ct.IsCancellationRequested)
            {
                await AppendLog(live, "WARN", "Operation cancelled by user");
                await PersistFinalState(live, IVF.Domain.Entities.BackupOperationStatus.Cancelled);
            }
            else if (process.ExitCode == 0)
            {
                await AppendLog(live, "OK", "Operation completed successfully");

                string? archiveName = null;
                if (scriptName.Contains("backup"))
                {
                    var newest = ListBackups().FirstOrDefault();
                    archiveName = newest?.FileName;
                }
                await PersistFinalState(live, IVF.Domain.Entities.BackupOperationStatus.Completed, archivePath: archiveName);
            }
            else
            {
                await AppendLog(live, "ERROR", $"Script exited with code {process.ExitCode}");
                await PersistFinalState(live, IVF.Domain.Entities.BackupOperationStatus.Failed, $"Script exited with code {process.ExitCode}");
            }
        }
        catch (OperationCanceledException)
        {
            await AppendLog(live, "WARN", "Operation cancelled");
            await PersistFinalState(live, IVF.Domain.Entities.BackupOperationStatus.Cancelled);
        }
        catch (Exception ex)
        {
            await AppendLog(live, "ERROR", $"Exception: {ex.Message}");
            logger.LogError(ex, "Backup/restore operation {OpCode} failed", live.OperationCode);
            await PersistFinalState(live, IVF.Domain.Entities.BackupOperationStatus.Failed, ex.Message);
        }
        finally
        {
            _cancellations.TryRemove(live.OperationCode, out _);
            _liveOps.TryRemove(live.OperationCode, out _);
        }
    }

    private async Task PersistFinalState(LiveOperation live, IVF.Domain.Entities.BackupOperationStatus status, string? errorMessage = null, string? archivePath = null)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            var entity = await db.BackupOperations.FindAsync(live.EntityId);
            if (entity == null) return;

            switch (status)
            {
                case IVF.Domain.Entities.BackupOperationStatus.Completed:
                    entity.MarkCompleted(archivePath);
                    break;
                case IVF.Domain.Entities.BackupOperationStatus.Failed:
                    entity.MarkFailed(errorMessage ?? "Unknown error");
                    break;
                case IVF.Domain.Entities.BackupOperationStatus.Cancelled:
                    entity.MarkCancelled();
                    break;
            }

            // Persist log lines as JSON
            entity.UpdateLogLines(JsonSerializer.Serialize(live.LogLines, JsonOpts));

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist final state for {OpCode}", live.OperationCode);
        }

        await NotifyStatusChanged(live, status.ToString(), errorMessage, archivePath);
    }

    private async Task ReadStreamAsync(StreamReader reader, LiveOperation live, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var level = ParseLogLevel(line);
            var message = StripAnsiAndPrefix(line);
            await AppendLog(live, level, message);
        }
    }

    private async Task AppendLog(LiveOperation live, string level, string message)
    {
        var logLine = new BackupLogLine
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message
        };
        live.LogLines.Add(logLine);

        await hubContext.Clients.Group(live.OperationCode).SendAsync("LogLine", new
        {
            operationId = live.OperationCode,
            logLine.Timestamp,
            logLine.Level,
            logLine.Message
        });
    }

    private async Task NotifyStatusChanged(LiveOperation live, string status, string? errorMessage = null, string? archivePath = null)
    {
        await hubContext.Clients.Group(live.OperationCode).SendAsync("StatusChanged", new
        {
            operationId = live.OperationCode,
            status,
            completedAt = status != "Running" ? DateTime.UtcNow : (DateTime?)null,
            errorMessage,
            archivePath
        });

        await hubContext.Clients.All.SendAsync("OperationUpdated", new
        {
            operationId = live.OperationCode,
            status,
            startedAt = DateTime.UtcNow,
            completedAt = status != "Running" ? DateTime.UtcNow : (DateTime?)null,
            archivePath
        });
    }

    private static string ParseLogLevel(string line)
    {
        if (line.Contains("[OK]")) return "OK";
        if (line.Contains("[ERROR]")) return "ERROR";
        if (line.Contains("[WARN]")) return "WARN";
        if (line.Contains("[INFO]")) return "INFO";
        return "INFO";
    }

    private static string StripAnsiAndPrefix(string line)
    {
        var stripped = System.Text.RegularExpressions.Regex.Replace(line, @"\x1B\[[0-9;]*m", "");
        stripped = System.Text.RegularExpressions.Regex.Replace(stripped, @"^\[(OK|INFO|WARN|ERROR)\]\s*", "");
        return stripped.Trim();
    }

    private static string? FindBash()
    {
        if (OperatingSystem.IsWindows())
        {
            var gitBash = @"C:\Program Files\Git\bin\bash.exe";
            if (File.Exists(gitBash)) return gitBash;

            var wslBash = @"C:\Windows\System32\wsl.exe";
            if (File.Exists(wslBash)) return wslBash;

            return null;
        }
        return "/bin/bash";
    }

    // ─── Cloud operations ───────────────────────────────────

    /// <summary>Upload a local backup archive to cloud storage, with optional Brotli compression.</summary>
    public async Task<CloudUploadResult> UploadToCloudAsync(string archiveFileName, CancellationToken ct = default)
    {
        var archivePath = Path.Combine(BackupsDir, archiveFileName);
        if (!File.Exists(archivePath))
            throw new FileNotFoundException($"Archive not found: {archiveFileName}");

        var cloudConfig = await cloudProviderFactory.GetConfigAsync(ct);
        var cloudProvider = await cloudProviderFactory.GetProviderAsync(ct);

        var originalSize = new FileInfo(archivePath).Length;
        string uploadFilePath = archivePath;
        string objectKey = $"backups/{archiveFileName}";
        CompressionResult? compression = null;

        try
        {
            // Compress before upload if enabled
            if (cloudConfig.CompressionEnabled)
            {
                compression = await compressionService.CompressAsync(archivePath, ct: ct);
                uploadFilePath = compression.CompressedFilePath;
                objectKey += BackupCompressionService.CompressedExtension;
            }

            var result = await cloudProvider.UploadAsync(uploadFilePath, objectKey, ct);

            // Record cloud upload on the latest matching operation
            await RecordCloudUploadAsync(archiveFileName, objectKey);

            return new CloudUploadResult(
                result.ObjectKey,
                result.SizeBytes,
                result.ETag,
                cloudProvider.ProviderName,
                compression?.OriginalSizeBytes ?? originalSize,
                compression?.CompressedSizeBytes,
                compression?.CompressionRatioPercent,
                compression?.DurationMs);
        }
        finally
        {
            // Cleanup temp compressed file
            if (compression != null && File.Exists(compression.CompressedFilePath))
            {
                try { File.Delete(compression.CompressedFilePath); } catch { /* ignore */ }
            }
        }
    }

    /// <summary>Download a backup from cloud storage, decompressing if Brotli-compressed.</summary>
    public async Task<string> DownloadFromCloudAsync(string objectKey, CancellationToken ct = default)
    {
        Directory.CreateDirectory(BackupsDir);
        var cloudProvider = await cloudProviderFactory.GetProviderAsync(ct);
        var localPath = await cloudProvider.DownloadAsync(objectKey, BackupsDir, ct);

        // Decompress if Brotli-compressed
        if (localPath.EndsWith(BackupCompressionService.CompressedExtension, StringComparison.OrdinalIgnoreCase))
        {
            var decompressedPath = await compressionService.DecompressAsync(localPath, ct: ct);
            try { File.Delete(localPath); } catch { /* ignore */ }
            return Path.GetFileName(decompressedPath);
        }

        return Path.GetFileName(localPath);
    }

    /// <summary>List all backup objects in cloud storage.</summary>
    public async Task<List<CloudBackupObject>> ListCloudBackupsAsync(CancellationToken ct = default)
    {
        var cloudProvider = await cloudProviderFactory.GetProviderAsync(ct);
        return await cloudProvider.ListAsync(ct);
    }

    /// <summary>Delete a backup from cloud storage.</summary>
    public async Task<bool> DeleteFromCloudAsync(string objectKey, CancellationToken ct = default)
    {
        var cloudProvider = await cloudProviderFactory.GetProviderAsync(ct);
        return await cloudProvider.DeleteAsync(objectKey, ct);
    }

    /// <summary>Test cloud provider connectivity.</summary>
    public async Task<CloudStatusResult> GetCloudStatusAsync(CancellationToken ct = default)
    {
        var cloudConfig = await cloudProviderFactory.GetConfigAsync(ct);
        var cloudProvider = await cloudProviderFactory.GetProviderAsync(ct);
        var connected = await cloudProvider.TestConnectionAsync(ct);
        List<CloudBackupObject>? objects = null;
        long totalSizeBytes = 0;

        if (connected)
        {
            objects = await cloudProvider.ListAsync(ct);
            totalSizeBytes = objects.Sum(o => o.SizeBytes);
        }

        return new CloudStatusResult(
            cloudProvider.ProviderName,
            connected,
            cloudConfig.CompressionEnabled,
            objects?.Count ?? 0,
            totalSizeBytes);
    }

    private async Task RecordCloudUploadAsync(string archiveFileName, string objectKey)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            var operation = await db.BackupOperations
                .Where(o => o.ArchivePath == archiveFileName && o.Status == IVF.Domain.Entities.BackupOperationStatus.Completed)
                .OrderByDescending(o => o.CompletedAt)
                .FirstOrDefaultAsync();

            if (operation != null)
            {
                operation.SetCloudUploaded(objectKey);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record cloud upload for {Archive}", archiveFileName);
        }
    }
}

public record CloudUploadResult(
    string ObjectKey,
    long CloudSizeBytes,
    string? ETag,
    string ProviderName,
    long OriginalSizeBytes,
    long? CompressedSizeBytes,
    double? CompressionRatioPercent,
    long? CompressionDurationMs);

public record CloudStatusResult(
    string ProviderName,
    bool Connected,
    bool CompressionEnabled,
    int BackupCount,
    long TotalSizeBytes);
