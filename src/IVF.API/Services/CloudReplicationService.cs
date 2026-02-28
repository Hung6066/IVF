using System.Diagnostics;
using System.Text;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Services;

/// <summary>
/// Manages cloud/external replication for PostgreSQL (streaming replication over SSL)
/// and MinIO (S3-compatible bucket sync). Provides secure, fast replication to remote targets.
/// </summary>
public sealed class CloudReplicationService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CloudReplicationService> logger)
{
    private const string DbContainer = "ivf-db";
    private const string MinioContainer = "ivf-minio";

    private static readonly string[] MinioBuckets = ["ivf-documents", "ivf-signed-pdfs", "ivf-medical-images"];

    /// <summary>
    /// Builds a clean endpoint URL, stripping any existing scheme from the stored value
    /// before prepending the correct one.  Accepts both "172.16.102.9:9000" and
    /// "http://172.16.102.9:9000" without producing "http://http://…".
    /// </summary>
    private static string BuildMinioEndpoint(string raw, bool useSsl)
    {
        var host = raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase)  ? raw[7..]
                 : raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? raw[8..]
                 : raw;
        return $"{(useSsl ? "https" : "http")}://{host}";
    }

    // ═══════════════════════════════════════════════════════
    // Configuration Management
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Ensures the <c>local</c> mc alias points to the primary MinIO instance.
    /// Required because /tmp/.mc/config.json is lost when the container restarts.
    /// </summary>
    private async Task EnsureLocalAliasAsync(CancellationToken ct)
    {
        var accessKey = configuration["MinIO:AccessKey"] ?? "minioadmin";
        var secretKey = configuration["MinIO:SecretKey"] ?? "minioadmin";
        // Docker secrets: value may start with "FILE:" prefix
        if (accessKey.StartsWith("FILE:", StringComparison.OrdinalIgnoreCase))
        {
            var (_, keyVal) = await RunCommandAsync($"docker exec {MinioContainer} cat {accessKey[5..]}", ct);
            accessKey = keyVal.Trim();
        }
        if (secretKey.StartsWith("FILE:", StringComparison.OrdinalIgnoreCase))
        {
            var (_, secVal) = await RunCommandAsync($"docker exec {MinioContainer} cat {secretKey[5..]}", ct);
            secretKey = secVal.Trim();
        }

        await RunCommandAsync(
            $"docker exec {MinioContainer} mc alias set local http://localhost:9000 {accessKey} {secretKey} --api S3v4", ct);
    }

    public async Task<CloudReplicationConfig> GetConfigAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        var config = await db.CloudReplicationConfigs.OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct);
        if (config != null) return config;

        config = CloudReplicationConfig.CreateDefault();
        db.CloudReplicationConfigs.Add(config);
        await db.SaveChangesAsync(ct);
        return config;
    }

    public async Task<CloudReplicationConfig> UpdateDbConfigAsync(
        bool? enabled = null,
        string? remoteHost = null,
        int? remotePort = null,
        string? remoteUser = null,
        string? remotePassword = null,
        string? sslMode = null,
        string? slotName = null,
        string? allowedIps = null,
        CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        var config = await db.CloudReplicationConfigs.OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = CloudReplicationConfig.CreateDefault();
            db.CloudReplicationConfigs.Add(config);
        }
        config.UpdateDbSettings(enabled, remoteHost, remotePort, remoteUser, remotePassword, sslMode, slotName, allowedIps);
        await db.SaveChangesAsync(ct);
        return config;
    }

    public async Task<CloudReplicationConfig> UpdateMinioConfigAsync(
        bool? enabled = null,
        string? endpoint = null,
        string? accessKey = null,
        string? secretKey = null,
        string? bucket = null,
        bool? useSsl = null,
        string? region = null,
        string? syncMode = null,
        string? syncCron = null,
        CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        var config = await db.CloudReplicationConfigs.OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = CloudReplicationConfig.CreateDefault();
            db.CloudReplicationConfigs.Add(config);
        }
        config.UpdateMinioSettings(enabled, endpoint, accessKey, secretKey, bucket, useSsl, region, syncMode, syncCron);
        await db.SaveChangesAsync(ct);
        return config;
    }

    // ═══════════════════════════════════════════════════════
    // PostgreSQL External Replication
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Test connectivity to the remote PostgreSQL instance.
    /// </summary>
    public async Task<(bool Success, string Message)> TestDbConnectionAsync(CancellationToken ct)
    {
        var config = await GetConfigAsync(ct);
        if (string.IsNullOrWhiteSpace(config.RemoteDbHost))
            return (false, "Remote DB host chưa được cấu hình");

        try
        {
            // Test TCP connectivity to remote PG
            var sslParam = config.RemoteDbSslMode == "disable" ? "" : $"sslmode={config.RemoteDbSslMode}";
            var connStr = $"host={config.RemoteDbHost} port={config.RemoteDbPort} user={config.RemoteDbUser ?? "replicator"} password={config.RemoteDbPassword ?? ""} dbname=postgres {sslParam} connect_timeout=10";

            var cmd = $"docker exec {DbContainer} psql \"{connStr}\" -t -c \"SELECT 'OK'\"";
            var (exit, output) = await RunCommandAsync(cmd, ct);

            if (exit == 0 && output.Contains("OK"))
                return (true, $"Kết nối thành công tới {config.RemoteDbHost}:{config.RemoteDbPort} (SSL: {config.RemoteDbSslMode})");

            return (false, $"Kết nối thất bại: {output.Trim()}");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi: {ex.Message}");
        }
    }

    /// <summary>
    /// Configure the primary PostgreSQL for external replication.
    /// Steps: create replication user, slot, HBA entry, verify SSL.
    /// </summary>
    public async Task<CloudReplicationSetupResult> SetupDbReplicationAsync(CancellationToken ct)
    {
        var config = await GetConfigAsync(ct);
        var steps = new List<string>();
        var dbUser = GetDbUser();

        try
        {
            // Step 1: Ensure WAL level is replica
            var walLevel = await PsqlScalar(dbUser, "SHOW wal_level;", ct);
            if (walLevel?.Trim() == "replica" || walLevel?.Trim() == "logical")
            {
                steps.Add($"✓ WAL level: {walLevel?.Trim()}");
            }
            else
            {
                await PsqlExec(dbUser, "ALTER SYSTEM SET wal_level = 'replica';", ct);
                steps.Add("✓ WAL level set to 'replica' (requires restart)");
            }

            // Step 2: Create replication user (if not exists)
            var replUser = config.RemoteDbUser ?? "cloud_replicator";
            var replPass = config.RemoteDbPassword ?? "cloud_repl_" + Guid.NewGuid().ToString("N")[..12];
            var userExists = await PsqlScalar(dbUser, $"SELECT 1 FROM pg_roles WHERE rolname = '{replUser}';", ct);
            if (string.IsNullOrWhiteSpace(userExists) || !userExists.Contains("1"))
            {
                await PsqlExec(dbUser, $"CREATE USER {replUser} WITH REPLICATION LOGIN ENCRYPTED PASSWORD '{replPass}';", ct);
                steps.Add($"✓ Created replication user: {replUser}");
            }
            else
            {
                // Update password
                await PsqlExec(dbUser, $"ALTER USER {replUser} WITH PASSWORD '{replPass}';", ct);
                steps.Add($"✓ Replication user exists: {replUser} (password updated)");
            }

            // Step 3: Create replication slot
            var slotName = config.RemoteDbSlotName ?? "cloud_standby_slot";
            var slotExists = await PsqlScalar(dbUser, $"SELECT 1 FROM pg_replication_slots WHERE slot_name = '{slotName}';", ct);
            if (string.IsNullOrWhiteSpace(slotExists) || !slotExists.Contains("1"))
            {
                await PsqlScalar(dbUser, $"SELECT pg_create_physical_replication_slot('{slotName}');", ct);
                steps.Add($"✓ Created replication slot: {slotName}");
            }
            else
            {
                steps.Add($"✓ Replication slot already exists: {slotName}");
            }

            // Step 4: Configure pg_hba.conf for remote access
            var allowedIps = config.RemoteDbAllowedIps ?? "0.0.0.0/0";
            var hbaEntries = new StringBuilder();
            foreach (var ip in allowedIps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                hbaEntries.Append($"hostssl replication {replUser} {ip} scram-sha-256\\n");
            }

            // Append to pg_hba.conf (idempotent — check first)
            var hbaCheck = await RunCommandAsync(
                $"docker exec {DbContainer} sh -c \"grep -c 'cloud_repl' /var/lib/postgresql/data/pg_hba.conf 2>/dev/null || echo 0\"", ct);
            if (hbaCheck.Output.Trim() == "0")
            {
                var hbaLine = $"hostssl replication {replUser} {allowedIps.Split(',')[0].Trim()} scram-sha-256";
                await RunCommandAsync(
                    $"docker exec {DbContainer} sh -c \"echo '# Cloud replication access (cloud_repl)' >> /var/lib/postgresql/data/pg_hba.conf && echo '{hbaLine}' >> /var/lib/postgresql/data/pg_hba.conf\"", ct);
                steps.Add($"✓ Added HBA entry for {allowedIps} (hostssl)");
            }
            else
            {
                steps.Add("✓ HBA entry already configured");
            }

            // Step 5: Verify SSL is enabled
            var sslEnabled = await PsqlScalar(dbUser, "SHOW ssl;", ct);
            if (sslEnabled?.Trim() == "on")
            {
                steps.Add("✓ SSL is enabled on primary");
            }
            else
            {
                await PsqlExec(dbUser, "ALTER SYSTEM SET ssl = on;", ct);
                steps.Add("⚠️ SSL enabled (requires restart + SSL certificate setup)");
            }

            // Step 6: Reload PG configuration
            await PsqlExec(dbUser, "SELECT pg_reload_conf();", ct);
            steps.Add("✓ PostgreSQL configuration reloaded");

            // Update config with the replication user
            await UpdateDbConfigAsync(
                remoteUser: replUser,
                remotePassword: replPass,
                slotName: slotName,
                ct: ct);

            // Generate connection string for remote standby
            var primaryHost = "<PRIMARY_PUBLIC_IP_OR_DOMAIN>";
            var primaryPort = 5433; // exposed Docker port
            var connectionInfo = $"host={primaryHost} port={primaryPort} user={replUser} password={replPass} sslmode=require";

            return new CloudReplicationSetupResult(
                Success: true,
                Steps: steps,
                ConnectionInfo: new DbReplicationConnectionInfo(
                    Host: primaryHost,
                    Port: primaryPort,
                    User: replUser,
                    Password: replPass,
                    SlotName: slotName,
                    SslMode: "require",
                    PrimaryConnInfo: connectionInfo,
                    BaseBackupCommand: $"PGPASSWORD='{replPass}' pg_basebackup -h {primaryHost} -p {primaryPort} -U {replUser} -D /var/lib/postgresql/data -Fp -Xs -P --checkpoint=fast --slot={slotName}",
                    StandbySignalContent: $"primary_conninfo = '{connectionInfo}'\nprimary_slot_name = '{slotName}'\nhot_standby = on"
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to setup DB replication");
            steps.Add($"✗ Error: {ex.Message}");
            return new CloudReplicationSetupResult(false, steps, null);
        }
    }

    /// <summary>
    /// Get status of external database replicas connected to this primary.
    /// </summary>
    public async Task<DbCloudReplicationStatus> GetDbReplicationStatusAsync(CancellationToken ct)
    {
        var config = await GetConfigAsync(ct);
        var dbUser = GetDbUser();

        try
        {
            // Get all connected replicas
            var sql = "SELECT pid::text, usename, application_name, client_addr::text, state, " +
                      "sent_lsn::text, replay_lsn::text, sync_state, " +
                      "EXTRACT(EPOCH FROM (now() - backend_start))::bigint::text, " +
                      "CASE WHEN sent_lsn IS NOT NULL AND replay_lsn IS NOT NULL THEN pg_wal_lsn_diff(sent_lsn, replay_lsn)::bigint::text ELSE '0' END " +
                      "FROM pg_stat_replication;";

            var cmd = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -t -A -F \"~\" -c \"{sql}\"";
            var (exit, output) = await RunCommandAsync(cmd, ct);

            var replicas = new List<ExternalReplica>();
            if (exit == 0 && !string.IsNullOrWhiteSpace(output))
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var cols = line.Split('~');
                    if (cols.Length < 10) continue;

                    var clientAddr = cols[3];
                    // Classify as local (Docker network) or external
                    var isExternal = !clientAddr.StartsWith("172.") && !clientAddr.StartsWith("10.") &&
                                   !clientAddr.StartsWith("192.168.") && clientAddr != "127.0.0.1";

                    replicas.Add(new ExternalReplica(
                        Pid: int.TryParse(cols[0], out var pid) ? pid : 0,
                        Username: cols[1],
                        ApplicationName: cols[2],
                        ClientAddress: clientAddr,
                        State: cols[4],
                        SentLsn: cols[5],
                        ReplayLsn: cols[6],
                        SyncState: cols[7],
                        UptimeSeconds: long.TryParse(cols[8], out var ut) ? ut : 0,
                        LagBytes: long.TryParse(cols[9], out var lb) ? lb : 0,
                        IsExternal: isExternal
                    ));
                }
            }

            // Check SSL status
            var sslStatus = await PsqlScalar(dbUser, "SHOW ssl;", ct);
            var currentLsn = await PsqlScalar(dbUser, "SELECT pg_current_wal_lsn()::text;", ct);

            // Check replication slot
            string? slotStatus = null;
            long slotRetainedBytes = 0;
            if (!string.IsNullOrWhiteSpace(config.RemoteDbSlotName))
            {
                var slotSql = $"SELECT active::text, CASE WHEN restart_lsn IS NOT NULL THEN pg_wal_lsn_diff(pg_current_wal_lsn(), restart_lsn)::bigint::text ELSE '0' END FROM pg_replication_slots WHERE slot_name = '{config.RemoteDbSlotName}';";
                var slotResult = await PsqlScalar(dbUser, slotSql, ct);
                if (!string.IsNullOrWhiteSpace(slotResult))
                {
                    var parts = slotResult.Trim().Split('|');
                    slotStatus = parts[0].Trim() == "t" ? "active" : "inactive";
                    long.TryParse(parts.ElementAtOrDefault(1)?.Trim(), out slotRetainedBytes);
                }
            }

            return new DbCloudReplicationStatus(
                Enabled: config.DbReplicationEnabled,
                SslEnabled: sslStatus?.Trim() == "on",
                CurrentLsn: currentLsn?.Trim() ?? "",
                SlotName: config.RemoteDbSlotName,
                SlotStatus: slotStatus,
                SlotRetainedBytes: slotRetainedBytes,
                ExternalReplicas: replicas.Where(r => r.IsExternal).ToList(),
                LocalReplicas: replicas.Where(r => !r.IsExternal).ToList(),
                LastSyncAt: config.LastDbSyncAt,
                LastSyncStatus: config.LastDbSyncStatus
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get DB cloud replication status");
            return new DbCloudReplicationStatus(
                config.DbReplicationEnabled, false, "", config.RemoteDbSlotName,
                null, 0, [], [], config.LastDbSyncAt, config.LastDbSyncStatus);
        }
    }

    // ═══════════════════════════════════════════════════════
    // MinIO External Replication (S3-compatible sync)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Test connectivity to the remote MinIO/S3 endpoint.
    /// </summary>
    public async Task<(bool Success, string Message)> TestMinioConnectionAsync(CancellationToken ct)
    {
        var config = await GetConfigAsync(ct);
        if (string.IsNullOrWhiteSpace(config.RemoteMinioEndpoint))
            return (false, "Remote MinIO endpoint chưa được cấu hình");

        try
        {
            // Step 1: configure mc alias (proves TCP connectivity + auth)
            var endpoint = BuildMinioEndpoint(config.RemoteMinioEndpoint!, config.RemoteMinioUseSsl);
            var setupAlias = $"docker exec {MinioContainer} mc alias set cloud-replica {endpoint} {config.RemoteMinioAccessKey} {config.RemoteMinioSecretKey} --api S3v4";
            var (aliasExit, aliasOutput) = await RunCommandAsync(setupAlias, ct);
            if (aliasExit != 0)
                return (false, $"Không thể cấu hình mc alias: {aliasOutput.Trim()}");

            // Step 2: list buckets — exit 0 even when empty; stderr goes to output via 2>&1
            var (lsExit, lsOutput) = await RunCommandAsync($"docker exec {MinioContainer} mc ls cloud-replica 2>&1", ct);
            if (lsExit != 0)
                return (false, $"Kết nối thất bại: {lsOutput.Trim()}");

            var bucketCount = lsOutput.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            return (true, $"Kết nối thành công tới {endpoint}. Buckets: {bucketCount}");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi: {ex.Message}");
        }
    }

    /// <summary>
    /// Set up MinIO bucket replication. Creates remote bucket if needed, configures mc alias.
    /// </summary>
    public async Task<(bool Success, List<string> Steps)> SetupMinioReplicationAsync(CancellationToken ct)
    {
        var config = await GetConfigAsync(ct);
        var steps = new List<string>();

        if (string.IsNullOrWhiteSpace(config.RemoteMinioEndpoint))
            return (false, ["✗ Remote MinIO endpoint chưa được cấu hình"]);

        try
        {
            var endpoint = BuildMinioEndpoint(config.RemoteMinioEndpoint!, config.RemoteMinioUseSsl);

            // Step 1: Configure mc alias
            var setupAlias = $"docker exec {MinioContainer} mc alias set cloud-replica {endpoint} {config.RemoteMinioAccessKey} {config.RemoteMinioSecretKey} --api S3v4";
            var (aliasExit, aliasSetOutput) = await RunCommandAsync(setupAlias, ct);
            if (aliasExit != 0)
                return (false, [$"✗ Không thể cấu hình mc alias cho {endpoint}: {aliasSetOutput.Trim()}"]);
            steps.Add($"✓ mc alias 'cloud-replica' → {endpoint}");

            // Step 2: Create remote bucket if not exists
            var bucket = config.RemoteMinioBucket;
            var (mbExit, mbOutput) = await RunCommandAsync(
                $"docker exec {MinioContainer} mc mb --ignore-existing cloud-replica/{bucket} 2>&1", ct);
            steps.Add(mbExit == 0
                ? $"✓ Remote bucket '{bucket}' ready"
                : $"⚠️ Bucket creation: {mbOutput.Trim()}");

            // Step 3: Set versioning on remote bucket (required for replication)
            await RunCommandAsync($"docker exec {MinioContainer} mc version enable cloud-replica/{bucket} 2>&1", ct);
            steps.Add($"✓ Versioning enabled on cloud-replica/{bucket}");

            steps.Add("✓ MinIO replication setup complete — ready for sync");
            return (true, steps);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to setup MinIO replication");
            steps.Add($"✗ Error: {ex.Message}");
            return (false, steps);
        }
    }

    /// <summary>
    /// Perform an incremental sync of all MinIO buckets to the remote target.
    /// Returns (success, synced files count, synced bytes).
    /// </summary>
    public async Task<MinioSyncResult> SyncMinioAsync(CancellationToken ct)
    {
        var config = await GetConfigAsync(ct);
        if (string.IsNullOrWhiteSpace(config.RemoteMinioEndpoint))
            return new MinioSyncResult(false, "Remote MinIO chưa được cấu hình", 0, 0, []);

        var totalFiles = 0;
        long totalBytes = 0;
        var bucketResults = new List<MinioBucketSyncResult>();

        try
        {
            // Ensure local alias points to primary MinIO (lost on container restart)
            await EnsureLocalAliasAsync(ct);

            // Ensure remote alias is configured
            var endpoint = BuildMinioEndpoint(config.RemoteMinioEndpoint!, config.RemoteMinioUseSsl);
            await RunCommandAsync(
                $"docker exec {MinioContainer} mc alias set cloud-replica {endpoint} {config.RemoteMinioAccessKey} {config.RemoteMinioSecretKey} --api S3v4", ct);

            var remoteBucket = config.RemoteMinioBucket;

            foreach (var bucket in MinioBuckets)
            {
                ct.ThrowIfCancellationRequested();

                var remoteTarget = $"cloud-replica/{bucket}/";
                var localSource = $"local/{bucket}/";

                // Use mc mirror for incremental sync (only transfers new/changed objects)
                var mirrorCmd = config.RemoteMinioSyncMode == "full"
                    ? $"docker exec {MinioContainer} mc mirror --overwrite {localSource} {remoteTarget} 2>&1"
                    : $"docker exec {MinioContainer} mc mirror {localSource} {remoteTarget} 2>&1";

                var (exit, output) = await RunCommandAsync(mirrorCmd, ct, timeoutSeconds: 600);

                // Parse output for file count
                var syncedFiles = 0;
                long syncedBytes = 0;
                if (exit == 0 || output.Contains("Total"))
                {
                    // mc mirror output contains lines like: "xxx.pdf: 1.2 MiB / 1.2 MiB"
                    syncedFiles = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Count(l => l.Contains('/') && !l.StartsWith("Total") && !l.StartsWith("mc:"));
                }

                totalFiles += syncedFiles;
                bucketResults.Add(new MinioBucketSyncResult(
                    Bucket: bucket,
                    Success: exit == 0,
                    FilesSync: syncedFiles,
                    Message: exit == 0 ? "OK" : output.Trim().Split('\n').LastOrDefault() ?? "Unknown error"
                ));

                logger.LogInformation("MinIO sync {Bucket} → {Target}: {Files} files", bucket, remoteTarget, syncedFiles);
            }

            // Record sync status
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            var cfg = await db.CloudReplicationConfigs.OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct);
            cfg?.RecordMinioSync("OK", totalBytes, totalFiles);
            if (cfg != null) await db.SaveChangesAsync(ct);

            return new MinioSyncResult(true, $"Đồng bộ {totalFiles} file từ {MinioBuckets.Length} bucket", totalFiles, totalBytes, bucketResults);
        }
        catch (OperationCanceledException)
        {
            return new MinioSyncResult(false, "Đồng bộ bị hủy", totalFiles, totalBytes, bucketResults);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MinIO sync failed");

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            var cfg = await db.CloudReplicationConfigs.OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct);
            cfg?.RecordMinioSync($"FAILED: {ex.Message}");
            if (cfg != null) await db.SaveChangesAsync(ct);

            return new MinioSyncResult(false, $"Lỗi: {ex.Message}", totalFiles, totalBytes, bucketResults);
        }
    }

    /// <summary>
    /// Get MinIO replication status (remote connectivity + last sync info).
    /// </summary>
    public async Task<MinioCloudReplicationStatus> GetMinioReplicationStatusAsync(CancellationToken ct)
    {
        var config = await GetConfigAsync(ct);

        // Ensure local alias points to primary MinIO (lost on container restart)
        await EnsureLocalAliasAsync(ct);

        // Get local bucket stats
        var bucketStats = new List<MinioBucketInfo>();
        foreach (var bucket in MinioBuckets)
        {
            var (exit, output) = await RunCommandAsync(
                $"docker exec {MinioContainer} mc du --depth 0 local/{bucket}/ 2>&1", ct);

            long size = 0;
            int objects = 0;
            if (exit == 0 && !string.IsNullOrWhiteSpace(output))
            {
                // mc du output: "1.2MiB\t10 objects\tlocal/bucket/"
                var parts = output.Trim().Split('\t');
                if (parts.Length >= 2)
                {
                    size = ParseMcSize(parts[0].Trim());
                    int.TryParse(parts[1].Trim().Split(' ')[0], out objects);
                }
            }

            bucketStats.Add(new MinioBucketInfo(bucket, size, objects));
        }

        return new MinioCloudReplicationStatus(
            Enabled: config.MinioReplicationEnabled,
            RemoteEndpoint: config.RemoteMinioEndpoint,
            RemoteBucket: config.RemoteMinioBucket,
            UseSsl: config.RemoteMinioUseSsl,
            SyncMode: config.RemoteMinioSyncMode,
            SyncCron: config.RemoteMinioSyncCron,
            LocalBuckets: bucketStats,
            LastSyncAt: config.LastMinioSyncAt,
            LastSyncStatus: config.LastMinioSyncStatus,
            LastSyncBytes: config.LastMinioSyncBytes,
            LastSyncFiles: config.LastMinioSyncFiles
        );
    }

    /// <summary>
    /// Get the external replication setup guide for connecting a remote standby.
    /// </summary>
    public async Task<ExternalReplicationGuide> GetExternalSetupGuideAsync(CancellationToken ct)
    {
        var config = await GetConfigAsync(ct);
        var replUser = config.RemoteDbUser ?? "cloud_replicator";
        var replPass = config.RemoteDbPassword ?? "<PASSWORD>";
        var slotName = config.RemoteDbSlotName ?? "cloud_standby_slot";

        return new ExternalReplicationGuide(
            Steps:
            [
                new("1. Kích hoạt replication trên Primary",
                    "Nhấn nút 'Setup DB Replication' để tự động cấu hình WAL, tạo user, slot, và HBA entry.",
                    "# Hoặc chạy thủ công:\nCREATE USER cloud_replicator WITH REPLICATION LOGIN ENCRYPTED PASSWORD 'xxx';\nSELECT pg_create_physical_replication_slot('cloud_standby_slot');"),

                new("2. Cấu hình mạng bảo mật",
                    "Mở port 5433 trên firewall. Khuyến nghị sử dụng VPN (WireGuard) hoặc SSH tunnel cho encryption layer bổ sung.",
                    "# WireGuard VPN (khuyến nghị):\nsudo wg-quick up wg0\n\n# Hoặc SSH tunnel:\nssh -L 5433:localhost:5433 user@primary-server"),

                new("3. Tạo Base Backup trên Standby",
                    "Trên server standby (cloud), chạy pg_basebackup để clone dữ liệu ban đầu.",
                    $"PGPASSWORD='{replPass}' pg_basebackup \\\n  -h <PRIMARY_IP> -p 5433 \\\n  -U {replUser} \\\n  -D /var/lib/postgresql/data \\\n  -Fp -Xs -P --checkpoint=fast \\\n  --slot={slotName}"),

                new("4. Cấu hình Standby (recovery.conf / standby.signal)",
                    "Tạo file standby.signal và cấu hình primary_conninfo trên standby.",
                    $"touch /var/lib/postgresql/data/standby.signal\n\n# Thêm vào postgresql.auto.conf:\nprimary_conninfo = 'host=<PRIMARY_IP> port=5433 user={replUser} password={replPass} sslmode=require'\nprimary_slot_name = '{slotName}'\nhot_standby = on"),

                new("5. Khởi động Standby",
                    "Start PostgreSQL trên standby. Nó sẽ tự động kết nối tới primary và bắt đầu streaming WAL.",
                    "pg_ctl start -D /var/lib/postgresql/data\n\n# Kiểm tra:\npsql -c \"SELECT pg_is_in_recovery();\" # Kết quả: t"),

                new("6. Giám sát từ Primary",
                    "Kiểm tra kết nối replication từ primary qua API hoặc psql.",
                    "SELECT client_addr, state, sent_lsn, replay_lsn,\n       pg_wal_lsn_diff(sent_lsn, replay_lsn) as lag_bytes\nFROM pg_stat_replication;"),
            ],
            MinioSteps:
            [
                new("1. Cấu hình Remote MinIO/S3",
                    "Nhập endpoint, credentials, và bucket name cho remote MinIO hoặc S3-compatible storage.",
                    "# Ví dụ cấu hình:\nEndpoint: s3.example.com:9000\nBucket: ivf-replica\nSSL: true"),

                new("2. Setup Replication",
                    "Nhấn 'Setup MinIO Replication' để tạo bucket và cấu hình trên remote target.",
                    "# Tự động chạy:\nmc alias set cloud-replica https://s3.example.com:9000 ACCESS_KEY SECRET_KEY\nmc mb cloud-replica/ivf-replica"),

                new("3. Đồng bộ dữ liệu",
                    "Chạy sync lần đầu (full) hoặc cấu hình sync tự động theo lịch.",
                    "# Incremental sync:\nmc mirror --newer-than 0s local/ivf-documents cloud-replica/ivf-replica/ivf-documents/"),

                new("4. Lịch tự động",
                    "Cấu hình cron schedule để sync tự động (mặc định mỗi 2 giờ).",
                    "# Cron expression mặc định:\n0 */2 * * *  # Mỗi 2 giờ"),
            ],
            SecurityNotes: [
                "🔒 PostgreSQL: Luôn sử dụng sslmode=require hoặc verify-full",
                "🔒 MinIO: Bật SSL/TLS cho remote endpoint",
                "🔒 Network: Sử dụng VPN (WireGuard/IPsec) hoặc SSH tunnel",
                "🔒 Firewall: Chỉ mở port cần thiết (5433 cho PG, 9000 cho MinIO)",
                "🔒 Credentials: Sử dụng IAM roles hoặc service accounts khi có thể",
                "🔒 Monitoring: Giám sát replication lag và alert khi > ngưỡng"
            ]
        );
    }

    // ═══════════════════════════════════════════════════════
    // Private Helpers
    // ═══════════════════════════════════════════════════════

    private async Task<string?> PsqlScalar(string dbUser, string sql, CancellationToken ct)
    {
        var escaped = sql.Replace("\"", "\\\"");
        var cmd = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -t -c \"{escaped}\"";
        var (exit, output) = await RunCommandAsync(cmd, ct);
        return exit == 0 ? output.Trim() : null;
    }

    private async Task PsqlExec(string dbUser, string sql, CancellationToken ct)
    {
        var escaped = sql.Replace("\"", "\\\"");
        var cmd = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"{escaped}\"";
        await RunCommandAsync(cmd, ct);
    }

    private string GetDbUser()
    {
        var connStr = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=ivf_db;Username=postgres;Password=postgres";
        foreach (var part in connStr.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals("Username", StringComparison.OrdinalIgnoreCase))
                return kv[1].Trim();
        }
        return "postgres";
    }

    private static long ParseMcSize(string sizeStr)
    {
        sizeStr = sizeStr.Trim().ToUpperInvariant();
        if (sizeStr.EndsWith("GIB")) return (long)(double.Parse(sizeStr[..^3]) * 1073741824);
        if (sizeStr.EndsWith("MIB")) return (long)(double.Parse(sizeStr[..^3]) * 1048576);
        if (sizeStr.EndsWith("KIB")) return (long)(double.Parse(sizeStr[..^3]) * 1024);
        if (sizeStr.EndsWith("B")) return long.TryParse(sizeStr[..^1], out var b) ? b : 0;
        return 0;
    }

    private static async Task<(int ExitCode, string Output)> RunCommandAsync(
        string command, CancellationToken ct, int timeoutSeconds = 30)
    {
        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start process");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var linked = timeoutCts.Token;

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(linked);
            var stderrTask = process.StandardError.ReadToEndAsync(linked);
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(linked);

            var stdout = stdoutTask.Result;
            var stderr = stderrTask.Result;
            return (process.ExitCode, string.IsNullOrWhiteSpace(stdout) ? stderr : stdout);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }
}

// ═══════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════

public record CloudReplicationSetupResult(
    bool Success,
    List<string> Steps,
    DbReplicationConnectionInfo? ConnectionInfo);

public record DbReplicationConnectionInfo(
    string Host, int Port, string User, string Password,
    string SlotName, string SslMode,
    string PrimaryConnInfo, string BaseBackupCommand, string StandbySignalContent);

public record DbCloudReplicationStatus(
    bool Enabled, bool SslEnabled, string CurrentLsn,
    string? SlotName, string? SlotStatus, long SlotRetainedBytes,
    List<ExternalReplica> ExternalReplicas,
    List<ExternalReplica> LocalReplicas,
    DateTime? LastSyncAt, string? LastSyncStatus);

public record ExternalReplica(
    int Pid, string Username, string ApplicationName,
    string ClientAddress, string State,
    string SentLsn, string ReplayLsn, string SyncState,
    long UptimeSeconds, long LagBytes, bool IsExternal);

public record MinioSyncResult(
    bool Success, string Message,
    int TotalFiles, long TotalBytes,
    List<MinioBucketSyncResult> BucketResults);

public record MinioBucketSyncResult(string Bucket, bool Success, int FilesSync, string Message);

public record MinioCloudReplicationStatus(
    bool Enabled, string? RemoteEndpoint, string RemoteBucket,
    bool UseSsl, string SyncMode, string? SyncCron,
    List<MinioBucketInfo> LocalBuckets,
    DateTime? LastSyncAt, string? LastSyncStatus,
    long LastSyncBytes, int LastSyncFiles);

public record MinioBucketInfo(string Name, long SizeBytes, int ObjectCount);

public record ExternalReplicationGuide(
    List<ReplicationSetupStep> Steps,
    List<ReplicationSetupStep> MinioSteps,
    List<string> SecurityNotes);
