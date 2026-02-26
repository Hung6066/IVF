using System.Diagnostics;

namespace IVF.API.Services;

/// <summary>
/// Monitors PostgreSQL streaming replication status and lag.
/// Provides real-time replication health information.
/// </summary>
public sealed class ReplicationMonitorService(
    IConfiguration configuration,
    ILogger<ReplicationMonitorService> logger)
{
    private const string DbContainer = "ivf-db";

    public async Task<ReplicationStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var dbUser = GetDbUser();

        try
        {
            // Single psql call for all settings + recovery status
            var sql = "SELECT pg_is_in_recovery()::text, current_setting('max_wal_senders'), current_setting('max_replication_slots'), current_setting('synchronous_standby_names'), CASE WHEN pg_is_in_recovery() THEN coalesce(pg_last_wal_replay_lsn()::text, '') ELSE pg_current_wal_lsn()::text END;";

            var cmd = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -t -A -F \"~\" -c \"{sql}\"";
            var (exit, output) = await RunCommandAsync(cmd, ct);

            string serverRole = "primary";
            int maxWalSenders = 0, maxReplSlots = 0;
            string synchronousStandby = "", currentLsn = "";

            if (exit == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var vals = output.Trim().Split('~');
                serverRole = vals.ElementAtOrDefault(0)?.Trim() == "t" ? "standby" : "primary";
                int.TryParse(vals.ElementAtOrDefault(1)?.Trim(), out maxWalSenders);
                int.TryParse(vals.ElementAtOrDefault(2)?.Trim(), out maxReplSlots);
                synchronousStandby = vals.ElementAtOrDefault(3)?.Trim() ?? "";
                currentLsn = vals.ElementAtOrDefault(4)?.Trim() ?? "";
            }

            // Get replication connections + slots (already single-call each)
            var replicas = await GetConnectedReplicasAsync(dbUser, ct);
            var slots = await GetReplicationSlotsAsync(dbUser, ct);

            return new ReplicationStatus(
                ServerRole: serverRole,
                IsReplicating: replicas.Count > 0,
                CurrentLsn: currentLsn,
                MaxWalSenders: maxWalSenders,
                MaxReplicationSlots: maxReplSlots,
                SynchronousStandbyNames: synchronousStandby,
                ConnectedReplicas: replicas,
                ReplicationSlots: slots
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get replication status");
            return new ReplicationStatus("unknown", false, "", 0, 0, "", [], []);
        }
    }

    public async Task<(bool Success, string Message)> CreateReplicationSlotAsync(string slotName, CancellationToken ct = default)
    {
        var dbUser = GetDbUser();

        try
        {
            var result = await PsqlScalar(dbUser,
                $"SELECT pg_create_physical_replication_slot('{slotName}')::text;", ct);
            return (true, $"Replication slot '{slotName}' created: {result?.Trim()}");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to create replication slot: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DropReplicationSlotAsync(string slotName, CancellationToken ct = default)
    {
        var dbUser = GetDbUser();

        try
        {
            await PsqlScalar(dbUser,
                $"SELECT pg_drop_replication_slot('{slotName}');", ct);
            return (true, $"Replication slot '{slotName}' dropped");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to drop replication slot: {ex.Message}");
        }
    }

    public ReplicationSetupGuide GetSetupGuide()
    {
        return new ReplicationSetupGuide(
            Steps:
            [
                new("1. Cấu hình Primary", "Bật WAL archiving và cấu hình max_wal_senders trên server chính",
                    "ALTER SYSTEM SET wal_level = 'replica';\nALTER SYSTEM SET max_wal_senders = 3;\nALTER SYSTEM SET max_replication_slots = 3;"),
                new("2. Tạo user replication", "Tạo user PostgreSQL có quyền replication",
                    "CREATE USER replicator WITH REPLICATION ENCRYPTED PASSWORD 'your_password';"),
                new("3. Cấu hình pg_hba.conf", "Cho phép kết nối replication từ standby server",
                    "host replication replicator standby_ip/32 md5"),
                new("4. Tạo base backup trên Standby", "Sao chép dữ liệu từ Primary sang Standby",
                    "pg_basebackup -h primary_host -D /var/lib/postgresql/data -U replicator -P --wal-method=stream"),
                new("5. Cấu hình Standby", "Tạo file standby.signal và cấu hình primary_conninfo",
                    "touch /var/lib/postgresql/data/standby.signal\n" +
                    "ALTER SYSTEM SET primary_conninfo = 'host=primary_host user=replicator password=your_password';"),
                new("6. Khởi động Standby", "Khởi động PostgreSQL trên Standby server",
                    "pg_ctl start -D /var/lib/postgresql/data"),
            ],
            DockerComposeExample: """
              db-standby:
                image: postgres:16-alpine
                container_name: ivf-db-standby
                environment:
                  - POSTGRES_USER=postgres
                  - POSTGRES_PASSWORD=postgres
                volumes:
                  - postgres_standby:/var/lib/postgresql/data
                ports:
                  - "5434:5432"
                networks:
                  - ivf-data
            """
        );
    }

    private async Task<List<ConnectedReplica>> GetConnectedReplicasAsync(string dbUser, CancellationToken ct)
    {
        var sql = "SELECT pid::text, usename, application_name, client_addr::text, state, sent_lsn::text, write_lsn::text, flush_lsn::text, replay_lsn::text, sync_state, EXTRACT(EPOCH FROM (now() - backend_start))::bigint::text, CASE WHEN sent_lsn IS NOT NULL AND replay_lsn IS NOT NULL THEN pg_wal_lsn_diff(sent_lsn, replay_lsn)::bigint::text ELSE '0' END FROM pg_stat_replication;";

        var cmd = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -t -A -F \"~\" -c \"{sql}\"";
        var (exit, output) = await RunCommandAsync(cmd, ct);

        if (exit != 0 || string.IsNullOrWhiteSpace(output)) return [];

        var replicas = new List<ConnectedReplica>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cols = line.Split('~');
            if (cols.Length < 12) continue;

            replicas.Add(new ConnectedReplica(
                Pid: int.TryParse(cols[0], out var pid) ? pid : 0,
                Username: cols[1],
                ApplicationName: cols[2],
                ClientAddress: cols[3],
                State: cols[4],
                SentLsn: cols[5],
                WriteLsn: cols[6],
                FlushLsn: cols[7],
                ReplayLsn: cols[8],
                SyncState: cols[9],
                UptimeSeconds: long.TryParse(cols[10], out var ut) ? ut : 0,
                LagBytes: long.TryParse(cols[11], out var lb) ? lb : 0
            ));
        }

        return replicas;
    }

    private async Task<List<ReplicationSlot>> GetReplicationSlotsAsync(string dbUser, CancellationToken ct)
    {
        var sql = "SELECT slot_name, slot_type, active::text, restart_lsn::text, confirmed_flush_lsn::text, CASE WHEN restart_lsn IS NOT NULL THEN pg_wal_lsn_diff(pg_current_wal_lsn(), restart_lsn)::bigint::text ELSE '0' END FROM pg_replication_slots;";

        var cmd = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -t -A -F \"~\" -c \"{sql}\"";
        var (exit, output) = await RunCommandAsync(cmd, ct);

        if (exit != 0 || string.IsNullOrWhiteSpace(output)) return [];

        var slots = new List<ReplicationSlot>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cols = line.Split('~');
            if (cols.Length < 6) continue;

            slots.Add(new ReplicationSlot(
                SlotName: cols[0],
                SlotType: cols[1],
                Active: cols[2] == "t",
                RestartLsn: cols[3],
                ConfirmedFlushLsn: cols[4],
                RetainedBytes: long.TryParse(cols[5], out var rb) ? rb : 0
            ));
        }

        return slots;
    }

    private async Task<string?> PsqlScalar(string dbUser, string sql, CancellationToken ct)
    {
        var cmd = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -t -c \"{sql.Replace("\"", "\\\"")}\"";
        var (exit, output) = await RunCommandAsync(cmd, ct);
        return exit == 0 ? output.Trim() : null;
    }

    private static async Task<(int ExitCode, string Output)> RunCommandAsync(string command, CancellationToken ct)
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

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        var linked = timeoutCts.Token;

        var stdoutTask = process.StandardOutput.ReadToEndAsync(linked);
        var stderrTask = process.StandardError.ReadToEndAsync(linked);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(linked);

        var stdout = stdoutTask.Result;
        var stderr = stderrTask.Result;
        var output = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
        return (process.ExitCode, output);
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
}

public record ReplicationStatus(
    string ServerRole,
    bool IsReplicating,
    string CurrentLsn,
    int MaxWalSenders,
    int MaxReplicationSlots,
    string SynchronousStandbyNames,
    List<ConnectedReplica> ConnectedReplicas,
    List<ReplicationSlot> ReplicationSlots);

public record ConnectedReplica(
    int Pid, string Username, string ApplicationName,
    string ClientAddress, string State,
    string SentLsn, string WriteLsn, string FlushLsn, string ReplayLsn,
    string SyncState, long UptimeSeconds, long LagBytes);

public record ReplicationSlot(
    string SlotName, string SlotType, bool Active,
    string RestartLsn, string ConfirmedFlushLsn, long RetainedBytes);

public record ReplicationSetupGuide(
    List<ReplicationSetupStep> Steps,
    string DockerComposeExample);

public record ReplicationSetupStep(string Title, string Description, string Command);
