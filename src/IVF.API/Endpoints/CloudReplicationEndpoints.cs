namespace IVF.API.Endpoints;

public static class CloudReplicationEndpoints
{
    public static void MapCloudReplicationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/data-backup/cloud-replication")
            .WithTags("Cloud Replication")
            .RequireAuthorization("AdminOnly");

        // ═══ Configuration ═══════════════════════════════════

        group.MapGet("/config", async (Services.CloudReplicationService service, CancellationToken ct) =>
        {
            var config = await service.GetConfigAsync(ct);
            return Results.Ok(new
            {
                // DB
                config.DbReplicationEnabled,
                config.RemoteDbHost,
                config.RemoteDbPort,
                config.RemoteDbUser,
                RemoteDbPassword = MaskSecret(config.RemoteDbPassword),
                config.RemoteDbSslMode,
                config.RemoteDbSlotName,
                config.RemoteDbAllowedIps,
                // MinIO
                config.MinioReplicationEnabled,
                config.RemoteMinioEndpoint,
                RemoteMinioAccessKey = MaskSecret(config.RemoteMinioAccessKey),
                RemoteMinioSecretKey = MaskSecret(config.RemoteMinioSecretKey),
                config.RemoteMinioBucket,
                config.RemoteMinioUseSsl,
                config.RemoteMinioRegion,
                config.RemoteMinioSyncMode,
                config.RemoteMinioSyncCron,
                // Status
                config.LastDbSyncAt,
                config.LastDbSyncStatus,
                config.LastMinioSyncAt,
                config.LastMinioSyncStatus,
                config.LastMinioSyncBytes,
                config.LastMinioSyncFiles,
            });
        })
        .WithName("GetCloudReplicationConfig");

        // ═══ DB Replication ══════════════════════════════════

        group.MapPut("/db/config", async (UpdateDbReplicationRequest req,
            Services.CloudReplicationService service, CancellationToken ct) =>
        {
            var config = await service.UpdateDbConfigAsync(
                enabled: req.Enabled,
                remoteHost: req.RemoteHost,
                remotePort: req.RemotePort,
                remoteUser: req.RemoteUser,
                remotePassword: IsMasked(req.RemotePassword) ? null : req.RemotePassword,
                sslMode: req.SslMode,
                slotName: req.SlotName,
                allowedIps: req.AllowedIps,
                ct: ct);
            return Results.Ok(new { message = "DB replication config updated" });
        })
        .WithName("UpdateDbReplicationConfig");

        group.MapPost("/db/test", async (Services.CloudReplicationService service, CancellationToken ct) =>
        {
            var (success, message) = await service.TestDbConnectionAsync(ct);
            return Results.Ok(new { success, message });
        })
        .WithName("TestDbReplicationConnection");

        group.MapPost("/db/setup", async (Services.CloudReplicationService service, CancellationToken ct) =>
        {
            var result = await service.SetupDbReplicationAsync(ct);
            return Results.Ok(result);
        })
        .WithName("SetupDbReplication");

        group.MapGet("/db/status", async (Services.CloudReplicationService service, CancellationToken ct) =>
        {
            var status = await service.GetDbReplicationStatusAsync(ct);
            return Results.Ok(status);
        })
        .WithName("GetDbReplicationStatus");

        // ═══ MinIO Replication ═══════════════════════════════

        group.MapPut("/minio/config", async (UpdateMinioReplicationRequest req,
            Services.CloudReplicationService service, CancellationToken ct) =>
        {
            var config = await service.UpdateMinioConfigAsync(
                enabled: req.Enabled,
                endpoint: req.Endpoint,
                accessKey: IsMasked(req.AccessKey) ? null : req.AccessKey,
                secretKey: IsMasked(req.SecretKey) ? null : req.SecretKey,
                bucket: req.Bucket,
                useSsl: req.UseSsl,
                region: req.Region,
                syncMode: req.SyncMode,
                syncCron: req.SyncCron,
                ct: ct);
            return Results.Ok(new { message = "MinIO replication config updated" });
        })
        .WithName("UpdateMinioReplicationConfig");

        group.MapPost("/minio/test", async (Services.CloudReplicationService service, CancellationToken ct) =>
        {
            var (success, message) = await service.TestMinioConnectionAsync(ct);
            return Results.Ok(new { success, message });
        })
        .WithName("TestMinioReplicationConnection");

        group.MapPost("/minio/setup", async (Services.CloudReplicationService service, CancellationToken ct) =>
        {
            var (success, steps) = await service.SetupMinioReplicationAsync(ct);
            return Results.Ok(new { success, steps });
        })
        .WithName("SetupMinioReplication");

        group.MapPost("/minio/sync", async (Services.CloudReplicationService service, CancellationToken ct) =>
        {
            var result = await service.SyncMinioAsync(ct);
            return Results.Ok(result);
        })
        .WithName("SyncMinioReplication");

        group.MapGet("/minio/status", async (Services.CloudReplicationService service, CancellationToken ct) =>
        {
            var status = await service.GetMinioReplicationStatusAsync(ct);
            return Results.Ok(status);
        })
        .WithName("GetMinioReplicationStatus");

        // ═══ Guide ═══════════════════════════════════════════

        group.MapGet("/guide", async (Services.CloudReplicationService service, CancellationToken ct) =>
        {
            var guide = await service.GetExternalSetupGuideAsync(ct);
            return Results.Ok(guide);
        })
        .WithName("GetCloudReplicationGuide");
    }

    private static string? MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length <= 4) return "****";
        return value[..2] + new string('*', Math.Min(value.Length - 4, 20)) + value[^2..];
    }

    private static bool IsMasked(string? value)
        => !string.IsNullOrEmpty(value) && value.Contains("****");
}

// ─── Request DTOs ────────────────────────────────────────

public record UpdateDbReplicationRequest(
    bool? Enabled = null,
    string? RemoteHost = null,
    int? RemotePort = null,
    string? RemoteUser = null,
    string? RemotePassword = null,
    string? SslMode = null,
    string? SlotName = null,
    string? AllowedIps = null);

public record UpdateMinioReplicationRequest(
    bool? Enabled = null,
    string? Endpoint = null,
    string? AccessKey = null,
    string? SecretKey = null,
    string? Bucket = null,
    bool? UseSsl = null,
    string? Region = null,
    string? SyncMode = null,
    string? SyncCron = null);
