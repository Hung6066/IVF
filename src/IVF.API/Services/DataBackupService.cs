using System.Collections.Concurrent;
using System.Text.Json;
using IVF.API.Hubs;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Services;

/// <summary>
/// Orchestrates database + MinIO data backup and restore operations.
/// Streams real-time logs via SignalR and persists to DB.
/// </summary>
public sealed class DataBackupService(
    IHubContext<BackupHub> hubContext,
    ILogger<DataBackupService> logger,
    IWebHostEnvironment env,
    IServiceScopeFactory scopeFactory,
    DatabaseBackupService dbBackupService,
    MinioBackupService minioBackupService,
    CloudBackupProviderFactory cloudProviderFactory,
    BackupCompressionService compressionService)
{
    private string ProjectDir => Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", ".."));
    private string BackupsDir => Path.Combine(ProjectDir, "backups");

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();
    private readonly ConcurrentDictionary<string, List<BackupLogLine>> _liveLogs = new();

    /// <summary>
    /// Get live log lines for a running data operation, or null if not tracked here.
    /// </summary>
    public List<BackupLogLine>? GetLiveLogs(string operationCode)
    {
        return _liveLogs.TryGetValue(operationCode, out var logs) ? [.. logs] : null;
    }

    /// <summary>
    /// Cancel a running data backup/restore operation.
    /// </summary>
    public bool CancelOperation(string operationCode)
    {
        if (_cancellations.TryGetValue(operationCode, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    // ─── Data backup ────────────────────────────────────────

    /// <summary>
    /// Start a full data backup (database + MinIO objects).
    /// Returns operation code for tracking.
    /// </summary>
    public async Task<string> StartDataBackupAsync(
        bool includeDatabase, bool includeMinio, bool uploadToCloud,
        string? startedBy, CancellationToken ct = default)
    {
        var operationCode = $"data_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
        var logLines = new List<BackupLogLine>();
        _liveLogs[operationCode] = logLines;

        var entity = BackupOperation.Create(operationCode, BackupOperationType.Backup, startedBy);

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            db.BackupOperations.Add(entity);
            await db.SaveChangesAsync(ct);
        }

        // Run in background with its own CTS for cancellation
        var cts = new CancellationTokenSource();
        _cancellations[operationCode] = cts;

        _ = Task.Run(async () =>
        {
            var bgCt = cts.Token;
            var files = new List<string>();
            try
            {
                await SendLog(operationCode, logLines, "INFO", "═══ Starting Data Backup ═══");
                await NotifyStatus(operationCode, "Running");
                Directory.CreateDirectory(BackupsDir);

                // Database backup
                if (includeDatabase)
                {
                    bgCt.ThrowIfCancellationRequested();
                    await SendLog(operationCode, logLines, "INFO", "── PostgreSQL Database Backup ──");
                    var (dbPath, dbSize) = await dbBackupService.BackupDatabaseAsync(
                        BackupsDir,
                        async (level, msg) => await SendLog(operationCode, logLines, level, msg),
                        bgCt);
                    files.Add(dbPath);
                }

                // MinIO backup
                if (includeMinio)
                {
                    bgCt.ThrowIfCancellationRequested();
                    await SendLog(operationCode, logLines, "INFO", "── MinIO Object Storage Backup ──");
                    var (minioPath, minioSize) = await minioBackupService.BackupMinioAsync(
                        BackupsDir,
                        async (level, msg) => await SendLog(operationCode, logLines, level, msg),
                        bgCt);
                    files.Add(minioPath);
                }

                // Upload to cloud if requested
                if (uploadToCloud && files.Count > 0)
                {
                    bgCt.ThrowIfCancellationRequested();
                    await SendLog(operationCode, logLines, "INFO", "── Uploading to Cloud ──");
                    var cloudConfig = await cloudProviderFactory.GetConfigAsync(bgCt);
                    var cloudProvider = await cloudProviderFactory.GetProviderAsync(bgCt);

                    foreach (var file in files)
                    {
                        bgCt.ThrowIfCancellationRequested();
                        var fileName = Path.GetFileName(file);
                        var objectKey = $"data-backups/{fileName}";

                        if (cloudConfig.CompressionEnabled)
                        {
                            await SendLog(operationCode, logLines, "INFO", $"Compressing {fileName}...");
                            var compressed = await compressionService.CompressAsync(file, ct: bgCt);
                            try
                            {
                                objectKey += BackupCompressionService.CompressedExtension;
                                await cloudProvider.UploadAsync(compressed.CompressedFilePath, objectKey, bgCt);
                                await SendLog(operationCode, logLines, "OK",
                                    $"Uploaded {fileName} to cloud (compressed {compressed.CompressionRatioPercent:F1}%)");
                            }
                            finally
                            {
                                try { File.Delete(compressed.CompressedFilePath); } catch { }
                            }
                        }
                        else
                        {
                            await cloudProvider.UploadAsync(file, objectKey, bgCt);
                            await SendLog(operationCode, logLines, "OK", $"Uploaded {fileName} to cloud");
                        }
                    }
                }

                var archiveNames = string.Join(", ", files.Select(Path.GetFileName));
                await SendLog(operationCode, logLines, "OK", $"═══ Data Backup Completed: {archiveNames} ═══");
                await PersistFinalState(operationCode, entity.Id, logLines, BackupOperationStatus.Completed, archiveNames);
                await NotifyStatus(operationCode, "Completed", archivePath: archiveNames);
            }
            catch (OperationCanceledException)
            {
                await SendLog(operationCode, logLines, "WARN", "Data backup cancelled by user");
                await PersistFinalState(operationCode, entity.Id, logLines, BackupOperationStatus.Cancelled);
                await NotifyStatus(operationCode, "Cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Data backup operation {OpCode} failed", operationCode);
                await SendLog(operationCode, logLines, "ERROR", $"Data backup failed: {ex.Message}");
                await PersistFinalState(operationCode, entity.Id, logLines, BackupOperationStatus.Failed, errorMessage: ex.Message);
                await NotifyStatus(operationCode, "Failed", errorMessage: ex.Message);
            }
            finally
            {
                _cancellations.TryRemove(operationCode, out _);
                cts.Dispose();
            }
        });

        return operationCode;
    }

    // ─── Data restore ───────────────────────────────────────

    /// <summary>
    /// Start a data restore (database and/or MinIO) from backup files.
    /// </summary>
    public async Task<string> StartDataRestoreAsync(
        string? databaseBackupFile, string? minioBackupFile,
        string? startedBy, CancellationToken ct = default)
    {
        var operationCode = $"data_restore_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
        var logLines = new List<BackupLogLine>();

        var entity = BackupOperation.Create(operationCode, BackupOperationType.Restore, startedBy);

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            db.BackupOperations.Add(entity);
            await db.SaveChangesAsync(ct);
        }

        // Run in background with its own CTS for cancellation
        var cts = new CancellationTokenSource();
        _cancellations[operationCode] = cts;

        _ = Task.Run(async () =>
        {
            var bgCt = cts.Token;
            try
            {
                await SendLog(operationCode, logLines, "INFO", "═══ Starting Data Restore ═══");
                await NotifyStatus(operationCode, "Running");

                // Database restore
                if (!string.IsNullOrEmpty(databaseBackupFile))
                {
                    bgCt.ThrowIfCancellationRequested();
                    var dbPath = Path.Combine(BackupsDir, databaseBackupFile);
                    if (!File.Exists(dbPath))
                        throw new FileNotFoundException($"Database backup not found: {databaseBackupFile}");

                    await SendLog(operationCode, logLines, "INFO", "── PostgreSQL Database Restore ──");
                    await dbBackupService.RestoreDatabaseAsync(
                        dbPath,
                        async (level, msg) => await SendLog(operationCode, logLines, level, msg),
                        bgCt);
                }

                // MinIO restore
                if (!string.IsNullOrEmpty(minioBackupFile))
                {
                    bgCt.ThrowIfCancellationRequested();
                    var minioPath = Path.Combine(BackupsDir, minioBackupFile);
                    if (!File.Exists(minioPath))
                        throw new FileNotFoundException($"MinIO backup not found: {minioBackupFile}");

                    await SendLog(operationCode, logLines, "INFO", "── MinIO Object Storage Restore ──");
                    await minioBackupService.RestoreMinioAsync(
                        minioPath,
                        async (level, msg) => await SendLog(operationCode, logLines, level, msg),
                        bgCt);
                }

                await SendLog(operationCode, logLines, "OK", "═══ Data Restore Completed ═══");
                await PersistFinalState(operationCode, entity.Id, logLines, BackupOperationStatus.Completed);
                await NotifyStatus(operationCode, "Completed");
            }
            catch (OperationCanceledException)
            {
                await SendLog(operationCode, logLines, "WARN", "Data restore cancelled by user");
                await PersistFinalState(operationCode, entity.Id, logLines, BackupOperationStatus.Cancelled);
                await NotifyStatus(operationCode, "Cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Data restore operation {OpCode} failed", operationCode);
                await SendLog(operationCode, logLines, "ERROR", $"Data restore failed: {ex.Message}");
                await PersistFinalState(operationCode, entity.Id, logLines, BackupOperationStatus.Failed, errorMessage: ex.Message);
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

    // ─── Status ─────────────────────────────────────────────

    /// <summary>
    /// Get combined status of database and MinIO storage.
    /// </summary>
    public async Task<DataBackupStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var dbInfo = await dbBackupService.GetDatabaseInfoAsync(ct);
        var minioInfo = await minioBackupService.GetMinioInfoAsync(ct);

        var dbBackups = dbBackupService.ListDatabaseBackups(BackupsDir);
        var minioBackups = minioBackupService.ListMinioBackups(BackupsDir);

        return new DataBackupStatus(dbInfo, minioInfo, dbBackups, minioBackups);
    }

    // ─── Helpers ────────────────────────────────────────────

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

    private async Task NotifyStatus(string operationCode, string status, string? errorMessage = null, string? archivePath = null)
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

    private async Task PersistFinalState(string operationCode, Guid entityId, List<BackupLogLine> logLines,
        BackupOperationStatus status, string? archivePath = null, string? errorMessage = null)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            var entity = await db.BackupOperations.FindAsync(entityId);

            if (entity == null)
            {
                // After a DB restore, the operation record may be gone (restored DB doesn't have it).
                // Re-insert so the history is preserved.
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
            logger.LogError(ex, "Failed to persist final state for data backup {OpCode}", operationCode);
        }
    }
}

public record DataBackupStatus(
    DatabaseInfo Database,
    MinioStorageInfo Minio,
    List<BackupInfo> DatabaseBackups,
    List<BackupInfo> MinioBackups);
