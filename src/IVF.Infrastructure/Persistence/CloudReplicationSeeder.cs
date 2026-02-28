using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// Seeds a default <see cref="CloudReplicationConfig"/> row that points to the external
/// replica server defined in docker-compose.replica.yml.
/// Idempotent — only runs when no row exists yet.
/// </summary>
public static class CloudReplicationSeeder
{
    // ── Remote replica defaults (match docker-compose.replica.yml + .env.replica.example)
    // Override these via the Admin → Cloud Replication UI after first boot.
    private const string RemoteHost = "172.16.102.11";
    private const int RemoteDbPort = 5201;
    private const string RemoteDbUser = "replicator";
    private const string RemoteDbPassword = "replicator_pass";   // must match REPLICATOR_PASSWORD in remote .env
    private const string RemoteDbSslMode = "disable";
    private const string ReplicationSlot = "cloud_standby_slot_2";

    private const string RemoteMinioEndpoint = "172.16.102.11:9000";
    private const string RemoteMinioAccessKey = "minioadmin";
    private const string RemoteMinioSecretKey = "minioadmin123";      // update after deploy
    private const string RemoteMinioBucket = "ivf-replica";
    private const string MinioSyncCron = "0 */2 * * *";    // every 2 hours

    public static async Task SeedAsync(IvfDbContext context)
    {
        if (await context.CloudReplicationConfigs.AnyAsync())
        {
            Console.WriteLine("[CloudReplicationSeeder] Config already exists. Skipping.");
            return;
        }

        Console.WriteLine("[CloudReplicationSeeder] Seeding default cloud replication config...");

        var config = CloudReplicationConfig.CreateDefault();

        // ── PostgreSQL streaming replication ─────────────────────────────────────
        config.UpdateDbSettings(
            enabled: false,           // enable via UI after verifying the standby is running
            remoteHost: RemoteHost,
            remotePort: RemoteDbPort,
            remoteUser: RemoteDbUser,
            remotePassword: RemoteDbPassword,
            sslMode: RemoteDbSslMode,
            slotName: ReplicationSlot,
            allowedIps: RemoteHost
        );

        // ── MinIO object-storage replication ─────────────────────────────────────
        config.UpdateMinioSettings(
            enabled: false,            // enable via UI after verifying MinIO is reachable
            endpoint: RemoteMinioEndpoint,
            accessKey: RemoteMinioAccessKey,
            secretKey: RemoteMinioSecretKey,
            bucket: RemoteMinioBucket,
            useSsl: false,            // plain HTTP for internal replica; set true when TLS is configured
            region: "us-east-1",
            syncMode: "incremental",
            syncCron: MinioSyncCron
        );

        await context.CloudReplicationConfigs.AddAsync(config);
        await context.SaveChangesAsync();

        Console.WriteLine($"[CloudReplicationSeeder] Seeded config → DB: {RemoteHost}:{RemoteDbPort}, MinIO: {RemoteMinioEndpoint}");
    }
}
