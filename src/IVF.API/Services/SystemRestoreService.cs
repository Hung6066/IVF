using System.Collections.Concurrent;
using System.Text.Json;
using IVF.API.Hubs;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Services;

/// <summary>
/// Orchestrates a full system restore: Database → MinIO → PKI (EJBCA + SignServer).
/// Streams real-time logs via SignalR. Each stage can be independently included/excluded.
/// </summary>
public sealed class SystemRestoreService(
    IHubContext<BackupHub> hubContext,
    ILogger<SystemRestoreService> logger,
    IWebHostEnvironment env,
    IServiceScopeFactory scopeFactory,
    DatabaseBackupService dbBackupService,
    MinioBackupService minioBackupService,
    BackupRestoreService pkiBackupService,
    WalBackupService walBackupService)
{
    private string ProjectDir => Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", ".."));
    private string BackupsDir => Path.Combine(ProjectDir, "backups");

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();
    private readonly ConcurrentDictionary<string, List<BackupLogLine>> _liveLogs = new();

    public List<BackupLogLine>? GetLiveLogs(string operationCode)
        => _liveLogs.TryGetValue(operationCode, out var logs) ? [.. logs] : null;

    public bool CancelOperation(string operationCode)
    {
        if (_cancellations.TryGetValue(operationCode, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get a pre-flight summary of what will be restored, with file sizes and validation.
    /// </summary>
    public async Task<SystemRestorePreflightResult> PreflightCheckAsync(
        string? databaseBackupFile,
        string? minioBackupFile,
        string? pkiBackupFile,
        string? baseBackupFile,
        string? pitrTargetTime,
        CancellationToken ct = default)
    {
        var stages = new List<RestoreStageInfo>();
        long totalSizeBytes = 0;

        // Database
        if (!string.IsNullOrEmpty(databaseBackupFile))
        {
            var path = Path.Combine(BackupsDir, databaseBackupFile);
            var exists = File.Exists(path);
            var size = exists ? new FileInfo(path).Length : 0;
            totalSizeBytes += size;
            stages.Add(new RestoreStageInfo("database", databaseBackupFile, exists, size, 1));
        }

        // PITR (alternative database restore)
        if (!string.IsNullOrEmpty(baseBackupFile))
        {
            var path = Path.Combine(BackupsDir, baseBackupFile);
            var exists = File.Exists(path);
            var size = exists ? new FileInfo(path).Length : 0;
            totalSizeBytes += size;
            stages.Add(new RestoreStageInfo("pitr", baseBackupFile, exists, size, 1,
                $"Target: {pitrTargetTime ?? "latest"}"));
        }

        // MinIO
        if (!string.IsNullOrEmpty(minioBackupFile))
        {
            var path = Path.Combine(BackupsDir, minioBackupFile);
            var exists = File.Exists(path);
            var size = exists ? new FileInfo(path).Length : 0;
            totalSizeBytes += size;
            stages.Add(new RestoreStageInfo("minio", minioBackupFile, exists, size, 2));
        }

        // PKI
        if (!string.IsNullOrEmpty(pkiBackupFile))
        {
            var path = Path.Combine(BackupsDir, pkiBackupFile);
            var exists = File.Exists(path);
            var size = exists ? new FileInfo(path).Length : 0;
            totalSizeBytes += size;
            stages.Add(new RestoreStageInfo("pki", pkiBackupFile, exists, size, 3));
        }

        var allFilesExist = stages.All(s => s.FileExists);
        var estimatedMinutes = stages.Count * 5 + (totalSizeBytes > 500_000_000 ? 10 : 0);

        return new SystemRestorePreflightResult(
            stages,
            allFilesExist,
            totalSizeBytes,
            estimatedMinutes);
    }

    /// <summary>
    /// Start a full system restore operation. Runs in background with live SignalR streaming.
    /// </summary>
    public async Task<string> StartSystemRestoreAsync(
        SystemRestoreRequest request,
        string? startedBy,
        CancellationToken ct = default)
    {
        var operationCode = $"system_restore_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
        var logLines = new List<BackupLogLine>();
        _liveLogs[operationCode] = logLines;

        var entity = BackupOperation.Create(operationCode, BackupOperationType.Restore, startedBy);

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            db.BackupOperations.Add(entity);
            await db.SaveChangesAsync(ct);
        }

        var cts = new CancellationTokenSource();
        _cancellations[operationCode] = cts;

        _ = Task.Run(async () =>
        {
            var bgCt = cts.Token;
            var completedStages = new List<string>();
            var totalStages = CountStages(request);
            var currentStage = 0;

            try
            {
                await SendLog(operationCode, logLines, "INFO",
                    $"═══ Starting Full System Restore ({totalStages} stage(s)) ═══");
                await NotifyStatus(operationCode, "Running");

                // ── Stage 1: Database Restore ──
                if (!string.IsNullOrEmpty(request.DatabaseBackupFile))
                {
                    currentStage++;
                    bgCt.ThrowIfCancellationRequested();
                    await SendLog(operationCode, logLines, "INFO",
                        $"── [{currentStage}/{totalStages}] PostgreSQL Database Restore ──");

                    var dbPath = Path.Combine(BackupsDir, request.DatabaseBackupFile);
                    if (!File.Exists(dbPath))
                        throw new FileNotFoundException($"Database backup not found: {request.DatabaseBackupFile}");

                    await dbBackupService.RestoreDatabaseAsync(
                        dbPath,
                        async (level, msg) => await SendLog(operationCode, logLines, level, msg),
                        bgCt);

                    completedStages.Add("database");
                    await SendLog(operationCode, logLines, "OK", "Database restore completed ✓");
                }

                // ── Stage 1-alt: PITR Restore ──
                if (!string.IsNullOrEmpty(request.BaseBackupFile))
                {
                    currentStage++;
                    bgCt.ThrowIfCancellationRequested();
                    await SendLog(operationCode, logLines, "INFO",
                        $"── [{currentStage}/{totalStages}] Point-in-Time Recovery ──");

                    var basePath = Path.Combine(BackupsDir, request.BaseBackupFile);
                    if (!File.Exists(basePath))
                        throw new FileNotFoundException($"Base backup not found: {request.BaseBackupFile}");

                    await walBackupService.RestoreFromPitrAsync(
                        basePath,
                        request.PitrTargetTime,
                        request.DryRun,
                        async (level, msg) => await SendLog(operationCode, logLines, level, msg),
                        bgCt);

                    completedStages.Add("pitr");
                    await SendLog(operationCode, logLines, "OK", "PITR restore completed ✓");
                }

                // ── Stage 2: MinIO Restore ──
                if (!string.IsNullOrEmpty(request.MinioBackupFile))
                {
                    currentStage++;
                    bgCt.ThrowIfCancellationRequested();
                    await SendLog(operationCode, logLines, "INFO",
                        $"── [{currentStage}/{totalStages}] MinIO Object Storage Restore ──");

                    var minioPath = Path.Combine(BackupsDir, request.MinioBackupFile);
                    if (!File.Exists(minioPath))
                        throw new FileNotFoundException($"MinIO backup not found: {request.MinioBackupFile}");

                    await minioBackupService.RestoreMinioAsync(
                        minioPath,
                        async (level, msg) => await SendLog(operationCode, logLines, level, msg),
                        bgCt);

                    completedStages.Add("minio");
                    await SendLog(operationCode, logLines, "OK", "MinIO restore completed ✓");
                }

                // ── Stage 3: PKI Restore ──
                if (!string.IsNullOrEmpty(request.PkiBackupFile))
                {
                    currentStage++;
                    bgCt.ThrowIfCancellationRequested();
                    await SendLog(operationCode, logLines, "INFO",
                        $"── [{currentStage}/{totalStages}] PKI (EJBCA + SignServer) Restore ──");

                    await pkiBackupService.StartRestoreAsync(
                        request.PkiBackupFile,
                        keysOnly: false,
                        dryRun: request.DryRun,
                        startedBy);

                    completedStages.Add("pki");
                    await SendLog(operationCode, logLines, "OK", "PKI restore completed ✓");
                }

                var summary = string.Join(", ", completedStages);
                var dryRunLabel = request.DryRun ? " (dry-run)" : "";
                await SendLog(operationCode, logLines, "OK",
                    $"═══ Full System Restore Completed{dryRunLabel}: {summary} ═══");

                await PersistFinalState(operationCode, entity.Id, logLines,
                    BackupOperationStatus.Completed, summary);
                await NotifyStatus(operationCode, "Completed", archivePath: summary);
            }
            catch (OperationCanceledException)
            {
                var partial = completedStages.Count > 0
                    ? $" (completed: {string.Join(", ", completedStages)})"
                    : "";
                await SendLog(operationCode, logLines, "WARN",
                    $"System restore cancelled by user{partial}");
                await PersistFinalState(operationCode, entity.Id, logLines,
                    BackupOperationStatus.Cancelled);
                await NotifyStatus(operationCode, "Cancelled");
            }
            catch (Exception ex)
            {
                var partial = completedStages.Count > 0
                    ? $" — completed stages: {string.Join(", ", completedStages)}"
                    : "";
                logger.LogError(ex, "System restore operation {OpCode} failed{Partial}",
                    operationCode, partial);
                await SendLog(operationCode, logLines, "ERROR",
                    $"System restore failed at stage {currentStage}/{totalStages}: {ex.Message}{partial}");
                await PersistFinalState(operationCode, entity.Id, logLines,
                    BackupOperationStatus.Failed, errorMessage: ex.Message);
                await NotifyStatus(operationCode, "Failed", errorMessage: ex.Message);
            }
            finally
            {
                _cancellations.TryRemove(operationCode, out _);
                _liveLogs.TryRemove(operationCode, out _);
                cts.Dispose();
            }
        });

        return operationCode;
    }

    /// <summary>
    /// Get available backup files for system restore, organized by category.
    /// </summary>
    public SystemRestoreInventory GetRestoreInventory()
    {
        var dbBackups = dbBackupService.ListDatabaseBackups(BackupsDir);
        var minioBackups = minioBackupService.ListMinioBackups(BackupsDir);
        var pkiBackups = pkiBackupService.ListBackups().ToList();
        var baseBackups = walBackupService.ListBaseBackups(BackupsDir);

        return new SystemRestoreInventory(dbBackups, minioBackups, pkiBackups, baseBackups);
    }

    private static int CountStages(SystemRestoreRequest r)
    {
        var count = 0;
        if (!string.IsNullOrEmpty(r.DatabaseBackupFile)) count++;
        if (!string.IsNullOrEmpty(r.BaseBackupFile)) count++;
        if (!string.IsNullOrEmpty(r.MinioBackupFile)) count++;
        if (!string.IsNullOrEmpty(r.PkiBackupFile)) count++;
        return count;
    }

    private async Task SendLog(string operationCode, List<BackupLogLine> logLines, string level, string message)
    {
        var logLine = new BackupLogLine
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message
        };
        logLines.Add(logLine);

        try
        {
            await hubContext.Clients.Group(operationCode).SendAsync("LogLine", new
            {
                operationId = operationCode,
                logLine.Timestamp,
                logLine.Level,
                logLine.Message
            });
        }
        catch { /* SignalR send failure — non-critical */ }
    }

    private async Task NotifyStatus(string operationCode, string status,
        string? errorMessage = null, string? archivePath = null)
    {
        try
        {
            await hubContext.Clients.Group(operationCode).SendAsync("StatusChanged", new
            {
                operationId = operationCode,
                status,
                completedAt = status != "Running" ? DateTime.UtcNow : (DateTime?)null,
                errorMessage,
                archivePath
            });

            await hubContext.Clients.All.SendAsync("OperationUpdated", new
            {
                operationId = operationCode,
                status,
                startedAt = DateTime.UtcNow,
                completedAt = status != "Running" ? DateTime.UtcNow : (DateTime?)null,
                archivePath
            });
        }
        catch { /* SignalR send failure — non-critical */ }
    }

    private async Task PersistFinalState(string operationCode, Guid entityId,
        List<BackupLogLine> logLines, BackupOperationStatus status,
        string? archivePath = null, string? errorMessage = null)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            var entity = await db.BackupOperations.FindAsync(entityId);

            if (entity == null)
            {
                entity = BackupOperation.Create(operationCode, BackupOperationType.Restore, null);
                db.BackupOperations.Add(entity);
            }

            switch (status)
            {
                case BackupOperationStatus.Completed:
                    entity.MarkCompleted(archivePath);
                    break;
                case BackupOperationStatus.Failed:
                    entity.MarkFailed(errorMessage ?? "Unknown error");
                    break;
                case BackupOperationStatus.Cancelled:
                    entity.MarkCancelled();
                    break;
            }

            entity.UpdateLogLines(JsonSerializer.Serialize(logLines, JsonOpts));
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist final state for system restore {OpCode}", operationCode);
        }
    }
}

// ─── Request / Response DTOs ────────────────────────────────

public record SystemRestoreRequest(
    string? DatabaseBackupFile = null,
    string? MinioBackupFile = null,
    string? PkiBackupFile = null,
    string? BaseBackupFile = null,
    string? PitrTargetTime = null,
    bool DryRun = false);

public record RestoreStageInfo(
    string Stage,
    string FileName,
    bool FileExists,
    long SizeBytes,
    int Order,
    string? Detail = null);

public record SystemRestorePreflightResult(
    List<RestoreStageInfo> Stages,
    bool AllFilesExist,
    long TotalSizeBytes,
    int EstimatedMinutes);

public record SystemRestoreInventory(
    List<BackupInfo> DatabaseBackups,
    List<BackupInfo> MinioBackups,
    IReadOnlyList<BackupInfo> PkiBackups,
    List<BackupInfo> BaseBackups);
