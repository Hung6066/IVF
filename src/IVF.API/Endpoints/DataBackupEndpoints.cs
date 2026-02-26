using IVF.API.Services;
using System.Security.Claims;

namespace IVF.API.Endpoints;

public static class DataBackupEndpoints
{
    public static void MapDataBackupEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/data-backup")
            .WithTags("Data Backup (DB + MinIO)")
            .RequireAuthorization("AdminOnly");

        // ─── Get data backup status ────────────────────────────
        group.MapGet("/status", async (DataBackupService service, CancellationToken ct) =>
        {
            var status = await service.GetStatusAsync(ct);
            return Results.Ok(new
            {
                database = new
                {
                    status.Database.DatabaseName,
                    status.Database.SizeBytes,
                    status.Database.TableCount,
                    status.Database.Connected,
                },
                minio = new
                {
                    status.Minio.TotalObjects,
                    status.Minio.TotalSizeBytes,
                    status.Minio.Connected,
                    buckets = status.Minio.Buckets.Select(b => new
                    {
                        b.Name,
                        b.ObjectCount,
                        b.SizeBytes
                    })
                },
                backups = new
                {
                    databaseBackups = status.DatabaseBackups.Select(b => new
                    {
                        b.FileName,
                        b.SizeBytes,
                        b.CreatedAt,
                        b.Checksum
                    }),
                    minioBackups = status.MinioBackups.Select(b => new
                    {
                        b.FileName,
                        b.SizeBytes,
                        b.CreatedAt,
                        b.Checksum
                    })
                }
            });
        })
        .WithName("GetDataBackupStatus");

        // ─── Start data backup ─────────────────────────────────
        group.MapPost("/start", async (DataBackupService service, HttpContext ctx, StartDataBackupRequest? request) =>
        {
            var username = ctx.User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
            var req = request ?? new StartDataBackupRequest();

            var operationCode = await service.StartDataBackupAsync(
                req.IncludeDatabase,
                req.IncludeMinio,
                req.UploadToCloud,
                username);

            return Results.Ok(new { operationId = operationCode });
        })
        .WithName("StartDataBackup");

        // ─── Start data restore ────────────────────────────────
        group.MapPost("/restore", async (DataBackupService service, HttpContext ctx, StartDataRestoreRequest request) =>
        {
            if (string.IsNullOrEmpty(request.DatabaseBackupFile) && string.IsNullOrEmpty(request.MinioBackupFile))
                return Results.BadRequest(new { error = "At least one backup file must be specified" });

            var username = ctx.User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

            var operationCode = await service.StartDataRestoreAsync(
                request.DatabaseBackupFile,
                request.MinioBackupFile,
                username);

            return Results.Ok(new { operationId = operationCode });
        })
        .WithName("StartDataRestore");

        // ─── Delete data backup file ───────────────────────────
        group.MapDelete("/{fileName}", (DataBackupService service, IWebHostEnvironment env, string fileName) =>
        {
            var projectDir = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", ".."));
            var backupsDir = Path.Combine(projectDir, "backups");
            var filePath = Path.Combine(backupsDir, fileName);

            // Validate file name matches expected patterns to prevent path traversal
            if (!fileName.StartsWith("ivf_db_") && !fileName.StartsWith("ivf_minio_"))
                return Results.BadRequest(new { error = "Invalid backup file name" });

            if (Path.GetFileName(filePath) != fileName)
                return Results.BadRequest(new { error = "Invalid file name" });

            if (!File.Exists(filePath))
                return Results.NotFound(new { error = "Backup file not found" });

            File.Delete(filePath);
            // Also delete the checksum sidecar if it exists
            var checksumPath = filePath + ".sha256";
            if (File.Exists(checksumPath)) File.Delete(checksumPath);
            return Results.Ok(new { message = $"Deleted {fileName}" });
        })
        .WithName("DeleteDataBackup");

        // ─── Start PITR restore ────────────────────────────────
        group.MapPost("/pitr-restore", async (DataBackupService service, HttpContext ctx, StartPitrRestoreRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.BaseBackupFile))
                return Results.BadRequest(new { error = "baseBackupFile is required" });

            var username = ctx.User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

            var operationCode = await service.StartPitrRestoreAsync(
                request.BaseBackupFile,
                request.TargetTime,
                request.DryRun,
                username);

            return Results.Ok(new { operationId = operationCode });
        })
        .WithName("StartPitrRestore");

        // ─── Validate backup file ──────────────────────────────
        group.MapPost("/validate", async (
            ValidateBackupRequest request,
            DatabaseBackupService dbService,
            MinioBackupService minioService,
            IWebHostEnvironment env,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.FileName))
                return Results.BadRequest(new { error = "fileName is required" });

            var projectDir = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", ".."));
            var backupsDir = Path.Combine(projectDir, "backups");
            var filePath = Path.Combine(backupsDir, request.FileName);

            if (Path.GetFileName(filePath) != request.FileName)
                return Results.BadRequest(new { error = "Invalid file name" });

            if (!File.Exists(filePath))
                return Results.NotFound(new { error = "Backup file not found" });

            if (request.FileName.StartsWith("ivf_db_"))
            {
                var result = await dbService.ValidateBackupAsync(filePath, ct: ct);
                return Results.Ok(new
                {
                    type = "database",
                    result.IsValid,
                    result.Error,
                    result.Checksum,
                    result.TableCount,
                    result.RowCount
                });
            }

            if (request.FileName.StartsWith("ivf_minio_"))
            {
                var result = await minioService.ValidateBackupAsync(filePath, ct: ct);
                return Results.Ok(new
                {
                    type = "minio",
                    result.IsValid,
                    result.Error,
                    result.Checksum,
                    result.BucketCount,
                    result.EntryCount
                });
            }

            return Results.BadRequest(new { error = "Unknown backup file type" });
        })
        .WithName("ValidateBackup");
    }
}

public record StartDataBackupRequest(
    bool IncludeDatabase = true,
    bool IncludeMinio = true,
    bool UploadToCloud = false);

public record StartDataRestoreRequest(
    string? DatabaseBackupFile = null,
    string? MinioBackupFile = null);

public record ValidateBackupRequest(string FileName);

public record StartPitrRestoreRequest(
    string BaseBackupFile,
    string? TargetTime = null,
    bool DryRun = false);
