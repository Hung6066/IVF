using IVF.API.Services;
using IVF.Application.Common.Interfaces;
using System.Security.Claims;

namespace IVF.API.Endpoints;

public static class BackupRestoreEndpoints
{
    public static void MapBackupRestoreEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/backup")
            .WithTags("Backup & Restore")
            .RequireAuthorization("AdminOnly");

        // ─── List available backups ─────────────────────────────
        group.MapGet("/archives", (BackupRestoreService service) =>
        {
            var backups = service.ListBackups();
            return Results.Ok(backups);
        })
        .WithName("ListBackupArchives");

        // ─── Start a backup ────────────────────────────────────
        group.MapPost("/start", async (BackupRestoreService service, HttpContext ctx, StartBackupRequest? request) =>
        {
            var username = ctx.User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
            var operationCode = await service.StartBackupAsync(request?.KeysOnly ?? false, username);
            return Results.Ok(new { operationId = operationCode });
        })
        .WithName("StartBackup");

        // ─── Start a restore ───────────────────────────────────
        group.MapPost("/restore", async (BackupRestoreService service, HttpContext ctx, StartRestoreRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.ArchiveFileName))
                return Results.BadRequest(new { error = "archiveFileName is required" });

            var username = ctx.User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
            try
            {
                var operationCode = await service.StartRestoreAsync(
                    request.ArchiveFileName,
                    request.KeysOnly,
                    request.DryRun,
                    username);
                return Results.Ok(new { operationId = operationCode });
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("StartRestore");

        // ─── Get operation status + logs ───────────────────────
        group.MapGet("/operations/{operationId}", async (BackupRestoreService service, string operationId) =>
        {
            var operation = await service.GetOperationByCodeAsync(operationId);
            if (operation == null)
                return Results.NotFound(new { error = "Operation not found" });

            var logLines = await service.GetLogsAsync(operationId);

            return Results.Ok(new
            {
                id = operation.OperationCode,
                type = operation.Type.ToString(),
                status = operation.Status.ToString(),
                operation.StartedAt,
                operation.CompletedAt,
                operation.ArchivePath,
                operation.ErrorMessage,
                operation.StartedBy,
                logLines = logLines.Select(l => new
                {
                    l.Timestamp,
                    l.Level,
                    l.Message
                })
            });
        })
        .WithName("GetBackupOperation");

        // ─── List operation history ────────────────────────────
        group.MapGet("/operations", async (BackupRestoreService service) =>
        {
            var operations = await service.GetOperationHistoryAsync();
            return Results.Ok(operations.Select(o => new
            {
                id = o.OperationCode,
                type = o.Type.ToString(),
                status = o.Status.ToString(),
                o.StartedAt,
                o.CompletedAt,
                o.ArchivePath,
                o.ErrorMessage,
                o.StartedBy,
                logLineCount = o.LogLinesJson?.Length ?? 0
            }));
        })
        .WithName("ListBackupOperations");

        // ─── Cancel running operation ──────────────────────────
        group.MapPost("/operations/{operationId}/cancel", (BackupRestoreService service, string operationId) =>
        {
            var cancelled = service.CancelOperation(operationId);
            return cancelled
                ? Results.Ok(new { message = "Cancellation requested" })
                : Results.NotFound(new { error = "Operation not found or already completed" });
        })
        .WithName("CancelBackupOperation");

        // ─── Get schedule config ───────────────────────────────
        group.MapGet("/schedule", async (BackupSchedulerService scheduler) =>
        {
            var config = await scheduler.GetConfigAsync();
            return Results.Ok(new
            {
                config.Enabled,
                config.CronExpression,
                config.KeysOnly,
                config.RetentionDays,
                config.MaxBackupCount,
                config.CloudSyncEnabled,
                nextScheduledRun = scheduler.NextScheduledRun,
                lastScheduledRun = config.LastScheduledRun,
                lastScheduledOperationId = config.LastScheduledOperationCode
            });
        })
        .WithName("GetBackupSchedule");

        // ─── Update schedule config ────────────────────────────
        group.MapPut("/schedule", async (BackupSchedulerService scheduler, UpdateScheduleRequest request) =>
        {
            if (request.CronExpression != null)
            {
                var next = BackupSchedulerService.GetNextCronTime(request.CronExpression, DateTime.UtcNow);
                if (next == null)
                    return Results.BadRequest(new { error = "Invalid cron expression. Use 5-field format: minute hour day-of-month month day-of-week" });
            }
            if (request.RetentionDays.HasValue && request.RetentionDays.Value < 1)
                return Results.BadRequest(new { error = "RetentionDays must be >= 1" });
            if (request.MaxBackupCount.HasValue && request.MaxBackupCount.Value < 1)
                return Results.BadRequest(new { error = "MaxBackupCount must be >= 1" });

            var config = await scheduler.UpdateConfigAsync(
                request.Enabled,
                request.CronExpression,
                request.KeysOnly,
                request.RetentionDays,
                request.MaxBackupCount,
                request.CloudSyncEnabled);

            return Results.Ok(new
            {
                message = "Schedule updated",
                config.Enabled,
                config.CronExpression,
                config.KeysOnly,
                config.RetentionDays,
                config.MaxBackupCount,
                config.CloudSyncEnabled
            });
        })
        .WithName("UpdateBackupSchedule");

        // ─── Manually trigger cleanup ──────────────────────────
        group.MapPost("/cleanup", async (BackupRestoreService service, BackupSchedulerService scheduler) =>
        {
            var config = await scheduler.GetConfigAsync();
            var backups = service.ListBackups();
            var deleted = new List<string>();

            if (config.RetentionDays > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-config.RetentionDays);
                foreach (var b in backups.Where(b => b.CreatedAt < cutoff))
                {
                    try { File.Delete(b.FullPath); deleted.Add(b.FileName); } catch { }
                }
            }

            if (config.MaxBackupCount > 0)
            {
                foreach (var b in backups.Skip(config.MaxBackupCount).Where(b => !deleted.Contains(b.FileName)))
                {
                    try { File.Delete(b.FullPath); deleted.Add(b.FileName); } catch { }
                }
            }

            return Results.Ok(new { deletedCount = deleted.Count, deletedFiles = deleted });
        })
        .WithName("CleanupOldBackups");

        // ═══ Cloud Backup Endpoints ═══════════════════════════════

        // ─── Get cloud config ──────────────────────────────────
        group.MapGet("/cloud/config", async (CloudBackupProviderFactory factory, CancellationToken ct) =>
        {
            var config = await factory.GetConfigAsync(ct);
            return Results.Ok(new CloudConfigResponse(
                config.Provider,
                config.CompressionEnabled,
                config.S3Region,
                config.S3BucketName,
                MaskSecret(config.S3AccessKey),
                MaskSecret(config.S3SecretKey),
                config.S3ServiceUrl,
                config.S3ForcePathStyle,
                MaskSecret(config.AzureConnectionString),
                config.AzureContainerName,
                config.GcsProjectId,
                config.GcsBucketName,
                config.GcsCredentialsPath));
        })
        .WithName("GetCloudConfig");

        // ─── Update cloud config ───────────────────────────────
        group.MapPut("/cloud/config", async (CloudBackupProviderFactory factory, UpdateCloudConfigRequest request, CancellationToken ct) =>
        {
            var validProviders = new[] { "MinIO", "S3", "Azure", "GCS" };
            if (request.Provider != null && !validProviders.Contains(request.Provider, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = $"Invalid provider. Must be one of: {string.Join(", ", validProviders)}" });

            // Don't overwrite secrets if masked placeholder is sent
            var s3AccessKey = IsMasked(request.S3AccessKey) ? null : request.S3AccessKey;
            var s3SecretKey = IsMasked(request.S3SecretKey) ? null : request.S3SecretKey;
            var azureConnStr = IsMasked(request.AzureConnectionString) ? null : request.AzureConnectionString;

            var config = await factory.UpdateConfigAsync(
                request.Provider,
                request.CompressionEnabled,
                request.S3Region,
                request.S3BucketName,
                s3AccessKey,
                s3SecretKey,
                request.S3ServiceUrl,
                request.S3ForcePathStyle,
                azureConnStr,
                request.AzureContainerName,
                request.GcsProjectId,
                request.GcsBucketName,
                request.GcsCredentialsPath,
                ct);

            return Results.Ok(new { message = "Cloud config updated", provider = config.Provider });
        })
        .WithName("UpdateCloudConfig");

        // ─── Test cloud connection (without saving) ────────────
        group.MapPost("/cloud/config/test", async (CloudBackupProviderFactory factory, TestCloudConfigRequest request, CancellationToken ct) =>
        {
            // If no provider specified, test current config
            if (string.IsNullOrEmpty(request.Provider))
            {
                var provider = await factory.GetProviderAsync(ct);
                var ok = await provider.TestConnectionAsync(ct);
                return Results.Ok(new { connected = ok, provider = provider.ProviderName });
            }

            // Build a temp config to test
            var tempConfig = IVF.Domain.Entities.CloudBackupConfig.CreateDefault();
            tempConfig.Update(
                provider: request.Provider,
                s3Region: request.S3Region,
                s3BucketName: request.S3BucketName,
                s3AccessKey: request.S3AccessKey,
                s3SecretKey: request.S3SecretKey,
                s3ServiceUrl: request.S3ServiceUrl,
                s3ForcePathStyle: request.S3ForcePathStyle,
                azureConnectionString: request.AzureConnectionString,
                azureContainerName: request.AzureContainerName,
                gcsProjectId: request.GcsProjectId,
                gcsBucketName: request.GcsBucketName,
                gcsCredentialsPath: request.GcsCredentialsPath);

            var cloudProvider = factory.CreateFromConfig(tempConfig);
            try
            {
                var connected = await cloudProvider.TestConnectionAsync(ct);
                return Results.Ok(new { connected, provider = cloudProvider.ProviderName });
            }
            finally
            {
                if (cloudProvider is IDisposable d) d.Dispose();
            }
        })
        .WithName("TestCloudConfig");

        // ─── Cloud status ──────────────────────────────────────
        group.MapGet("/cloud/status", async (BackupRestoreService service, CancellationToken ct) =>
        {
            var status = await service.GetCloudStatusAsync(ct);
            return Results.Ok(status);
        })
        .WithName("GetCloudBackupStatus");

        // ─── List cloud backups ────────────────────────────────
        group.MapGet("/cloud/list", async (BackupRestoreService service, CancellationToken ct) =>
        {
            var objects = await service.ListCloudBackupsAsync(ct);
            return Results.Ok(objects);
        })
        .WithName("ListCloudBackups");

        // ─── Upload to cloud ───────────────────────────────────
        group.MapPost("/cloud/upload", async (BackupRestoreService service, CloudUploadRequest request, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.ArchiveFileName))
                return Results.BadRequest(new { error = "archiveFileName is required" });

            try
            {
                var result = await service.UploadToCloudAsync(request.ArchiveFileName, ct);
                return Results.Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("UploadToCloud");

        // ─── Download from cloud ───────────────────────────────
        group.MapPost("/cloud/download", async (BackupRestoreService service, CloudDownloadRequest request, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.ObjectKey))
                return Results.BadRequest(new { error = "objectKey is required" });

            var fileName = await service.DownloadFromCloudAsync(request.ObjectKey, ct);
            return Results.Ok(new { fileName, message = "Downloaded and decompressed successfully" });
        })
        .WithName("DownloadFromCloud");

        // ─── Delete from cloud ─────────────────────────────────
        group.MapDelete("/cloud/{**objectKey}", async (BackupRestoreService service, string objectKey, CancellationToken ct) =>
        {
            var deleted = await service.DeleteFromCloudAsync(objectKey, ct);
            return deleted
                ? Results.Ok(new { message = "Deleted from cloud" })
                : Results.NotFound(new { error = "Object not found" });
        })
        .WithName("DeleteFromCloud");
    }

    private static string? MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length <= 4) return "****";
        return value[..2] + new string('*', value.Length - 4) + value[^2..];
    }

    private static bool IsMasked(string? value)
    {
        return value != null && value.Contains("****");
    }
}

public record StartBackupRequest(bool KeysOnly = false);
public record StartRestoreRequest(string ArchiveFileName, bool KeysOnly = false, bool DryRun = false);
public record UpdateScheduleRequest(bool? Enabled, string? CronExpression, bool? KeysOnly, int? RetentionDays, int? MaxBackupCount, bool? CloudSyncEnabled);
public record CloudUploadRequest(string ArchiveFileName);
public record CloudDownloadRequest(string ObjectKey);

public record UpdateCloudConfigRequest(
    string? Provider,
    bool? CompressionEnabled,
    string? S3Region,
    string? S3BucketName,
    string? S3AccessKey,
    string? S3SecretKey,
    string? S3ServiceUrl,
    bool? S3ForcePathStyle,
    string? AzureConnectionString,
    string? AzureContainerName,
    string? GcsProjectId,
    string? GcsBucketName,
    string? GcsCredentialsPath);

public record TestCloudConfigRequest(
    string? Provider = null,
    string? S3Region = null,
    string? S3BucketName = null,
    string? S3AccessKey = null,
    string? S3SecretKey = null,
    string? S3ServiceUrl = null,
    bool? S3ForcePathStyle = null,
    string? AzureConnectionString = null,
    string? AzureContainerName = null,
    string? GcsProjectId = null,
    string? GcsBucketName = null,
    string? GcsCredentialsPath = null);

public record CloudConfigResponse(
    string Provider,
    bool CompressionEnabled,
    string S3Region,
    string S3BucketName,
    string? S3AccessKey,
    string? S3SecretKey,
    string? S3ServiceUrl,
    bool S3ForcePathStyle,
    string? AzureConnectionString,
    string AzureContainerName,
    string? GcsProjectId,
    string GcsBucketName,
    string? GcsCredentialsPath);
