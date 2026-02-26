using IVF.API.Services;

namespace IVF.API.Endpoints;

public static class BackupComplianceEndpoints
{
    public static void MapBackupComplianceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/data-backup")
            .WithTags("Backup 3-2-1 Compliance")
            .RequireAuthorization("AdminOnly");

        // ─── Compliance Dashboard ─────────────────────────
        group.MapGet("/compliance", async (BackupComplianceService service, CancellationToken ct) =>
        {
            var report = await service.EvaluateAsync(ct);
            return Results.Ok(report);
        })
        .WithName("GetBackupCompliance");

        // ─── WAL Status ──────────────────────────────────
        group.MapGet("/wal/status", async (WalBackupService service, CancellationToken ct) =>
        {
            var status = await service.GetWalStatusAsync(ct);
            var archive = await service.GetArchiveInfoAsync(ct);
            return Results.Ok(new { wal = status, archive });
        })
        .WithName("GetWalStatus");

        group.MapPost("/wal/enable", async (WalBackupService service, CancellationToken ct) =>
        {
            var (success, message) = await service.EnableWalArchivingAsync(ct);
            return success ? Results.Ok(new { message }) : Results.BadRequest(new { error = message });
        })
        .WithName("EnableWalArchiving");

        group.MapPost("/wal/switch", async (WalBackupService service, CancellationToken ct) =>
        {
            var (success, message) = await service.SwitchWalAsync(ct);
            return success ? Results.Ok(new { message }) : Results.BadRequest(new { error = message });
        })
        .WithName("SwitchWal");

        group.MapPost("/wal/base-backup", async (
            WalBackupService service,
            IWebHostEnvironment env,
            CancellationToken ct) =>
        {
            var backupsDir = Path.Combine(
                Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..")), "backups");

            var result = await service.CreateBaseBackupAsync(backupsDir, ct: ct);
            if (result == null)
                return Results.BadRequest(new { error = "Base backup failed" });

            return Results.Ok(new
            {
                fileName = Path.GetFileName(result.Value.FilePath),
                sizeBytes = result.Value.SizeBytes,
                message = "Base backup created successfully"
            });
        })
        .WithName("CreateBaseBackup");

        group.MapGet("/wal/base-backups", (WalBackupService service, IWebHostEnvironment env) =>
        {
            var backupsDir = Path.Combine(
                Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..")), "backups");
            var backups = service.ListBaseBackups(backupsDir);
            return Results.Ok(backups.Select(b => new
            {
                b.FileName,
                b.SizeBytes,
                b.CreatedAt,
                b.Checksum
            }));
        })
        .WithName("ListBaseBackups");

        // ─── Replication Status ──────────────────────────
        group.MapGet("/replication/status", async (ReplicationMonitorService service, CancellationToken ct) =>
        {
            var status = await service.GetStatusAsync(ct);
            return Results.Ok(status);
        })
        .WithName("GetReplicationStatus");

        group.MapGet("/replication/guide", (ReplicationMonitorService service) =>
        {
            var guide = service.GetSetupGuide();
            return Results.Ok(guide);
        })
        .WithName("GetReplicationGuide");

        group.MapPost("/replication/slots", async (
            CreateReplicationSlotRequest request,
            ReplicationMonitorService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.SlotName))
                return Results.BadRequest(new { error = "slotName is required" });

            // Validate slot name: only alphanumeric and underscores
            if (!System.Text.RegularExpressions.Regex.IsMatch(request.SlotName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                return Results.BadRequest(new { error = "Invalid slot name. Use only letters, digits, and underscores." });

            var (success, message) = await service.CreateReplicationSlotAsync(request.SlotName, ct);
            return success ? Results.Ok(new { message }) : Results.BadRequest(new { error = message });
        })
        .WithName("CreateReplicationSlot");

        group.MapDelete("/replication/slots/{slotName}", async (
            string slotName,
            ReplicationMonitorService service,
            CancellationToken ct) =>
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(slotName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                return Results.BadRequest(new { error = "Invalid slot name" });

            var (success, message) = await service.DropReplicationSlotAsync(slotName, ct);
            return success ? Results.Ok(new { message }) : Results.BadRequest(new { error = message });
        })
        .WithName("DropReplicationSlot");

        // ─── Replication Activation ─────────────────────────
        group.MapPost("/replication/activate", async (
            WalBackupService walService,
            ReplicationMonitorService replService,
            CancellationToken ct) =>
        {
            var steps = new List<string>();

            // Step 1: Enable WAL archiving (required for replication)
            var (walOk, walMsg) = await walService.EnableWalArchivingAsync(ct);
            steps.Add($"WAL archiving: {walMsg}");

            // Step 2: Create standby replication slot
            var (slotOk, slotMsg) = await replService.CreateReplicationSlotAsync("standby_slot", ct);
            steps.Add($"Replication slot: {slotMsg}");

            return Results.Ok(new
            {
                success = walOk,
                steps,
                nextAction = "Run 'docker compose --profile replication up -d' to start the standby, then restart the primary with 'docker restart ivf-db'"
            });
        })
        .WithName("ActivateReplication");

        // ─── WAL Archive Listing ─────────────────────────────
        group.MapGet("/wal/archives", (IWebHostEnvironment env) =>
        {
            var walDir = Path.Combine(
                Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..")), "backups", "wal");

            if (!Directory.Exists(walDir))
                return Results.Ok(new { files = Array.Empty<object>(), totalCount = 0, totalSizeBytes = 0L });

            var files = Directory.GetFiles(walDir)
                .Where(f => !f.EndsWith(".sha256") && !f.EndsWith(".br"))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => new
                {
                    fileName = f.Name,
                    sizeBytes = f.Length,
                    createdAt = f.LastWriteTimeUtc,
                    checksum = BackupIntegrityService.LoadStoredChecksum(f.FullName)
                })
                .ToList();

            return Results.Ok(new
            {
                files,
                totalCount = files.Count,
                totalSizeBytes = files.Sum(f => f.sizeBytes)
            });
        })
        .WithName("ListWalArchives");
    }
}

public record CreateReplicationSlotRequest(string SlotName);
