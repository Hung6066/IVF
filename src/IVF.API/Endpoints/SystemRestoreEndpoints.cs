using IVF.API.Services;
using System.Security.Claims;

namespace IVF.API.Endpoints;

public static class SystemRestoreEndpoints
{
    public static void MapSystemRestoreEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/system-restore")
            .WithTags("System Restore (Full)")
            .RequireAuthorization("AdminOnly");

        // ─── Preflight check ──────────────────────────────
        group.MapPost("/preflight", async (
            SystemRestoreService service,
            SystemRestorePreflightRequest request,
            CancellationToken ct) =>
        {
            var result = await service.PreflightCheckAsync(
                request.DatabaseBackupFile,
                request.MinioBackupFile,
                request.PkiBackupFile,
                request.BaseBackupFile,
                request.PitrTargetTime,
                ct);

            return Results.Ok(result);
        })
        .WithName("SystemRestorePreflight");

        // ─── Inventory of available backups ───────────────
        group.MapGet("/inventory", (SystemRestoreService service) =>
        {
            var inventory = service.GetRestoreInventory();
            return Results.Ok(new
            {
                database = inventory.DatabaseBackups.Select(b => new
                {
                    b.FileName,
                    b.SizeBytes,
                    b.CreatedAt,
                    b.Checksum
                }),
                minio = inventory.MinioBackups.Select(b => new
                {
                    b.FileName,
                    b.SizeBytes,
                    b.CreatedAt,
                    b.Checksum
                }),
                pki = inventory.PkiBackups.Select(b => new
                {
                    b.FileName,
                    b.SizeBytes,
                    b.CreatedAt,
                    b.Checksum
                }),
                baseBackups = inventory.BaseBackups.Select(b => new
                {
                    b.FileName,
                    b.SizeBytes,
                    b.CreatedAt,
                    b.Checksum
                })
            });
        })
        .WithName("SystemRestoreInventory");

        // ─── Start full system restore ────────────────────
        group.MapPost("/start", async (
            SystemRestoreService service,
            HttpContext ctx,
            SystemRestoreRequest request) =>
        {
            var hasAnyFile = !string.IsNullOrEmpty(request.DatabaseBackupFile)
                || !string.IsNullOrEmpty(request.MinioBackupFile)
                || !string.IsNullOrEmpty(request.PkiBackupFile)
                || !string.IsNullOrEmpty(request.BaseBackupFile);

            if (!hasAnyFile)
                return Results.BadRequest(new { error = "Ít nhất một tệp backup phải được chỉ định" });

            var username = ctx.User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
            var operationCode = await service.StartSystemRestoreAsync(request, username);

            return Results.Ok(new { operationId = operationCode });
        })
        .WithName("StartSystemRestore");

        // ─── Get live logs for running restore ────────────
        group.MapGet("/logs/{operationCode}", (SystemRestoreService service, string operationCode) =>
        {
            var logs = service.GetLiveLogs(operationCode);
            return logs != null
                ? Results.Ok(logs)
                : Results.NotFound(new { error = "Operation not found or already completed" });
        })
        .WithName("GetSystemRestoreLogs");

        // ─── Cancel running restore ───────────────────────
        group.MapPost("/cancel/{operationCode}", (SystemRestoreService service, string operationCode) =>
        {
            var cancelled = service.CancelOperation(operationCode);
            return cancelled
                ? Results.Ok(new { message = "Cancellation requested" })
                : Results.NotFound(new { error = "Operation not found" });
        })
        .WithName("CancelSystemRestore");
    }
}

public record SystemRestorePreflightRequest(
    string? DatabaseBackupFile = null,
    string? MinioBackupFile = null,
    string? PkiBackupFile = null,
    string? BaseBackupFile = null,
    string? PitrTargetTime = null);
