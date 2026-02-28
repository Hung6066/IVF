using System.Diagnostics;

namespace IVF.API.Services;

/// <summary>
/// Service for managing PostgreSQL WAL (Write-Ahead Log) archiving and base backups.
/// Provides point-in-time recovery (PITR) capabilities.
/// </summary>
public sealed class WalBackupService(
    IConfiguration configuration,
    BackupIntegrityService integrityService,
    ILogger<WalBackupService> logger)
{
    private const string DbContainer = "ivf-db";

    public async Task<WalStatus> GetWalStatusAsync(CancellationToken ct = default)
    {
        var dbUser = GetDbUser();

        try
        {
            // Single psql call — all on one line to avoid cmd.exe escaping issues
            var sql = "SELECT current_setting('wal_level'), current_setting('archive_mode'), current_setting('archive_command'), current_setting('archive_timeout'), current_setting('wal_segment_size'), pg_current_wal_lsn()::text, pg_wal_lsn_diff(pg_current_wal_lsn(), '0/0')::bigint, a.last_archived_wal, a.last_archived_time::text, a.last_failed_wal, a.last_failed_time::text, a.archived_count, a.failed_count FROM pg_stat_archiver a;";

            var cmd = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -t -A -F \"^\" -c \"{sql}\"";
            var (exit, output) = await RunCommandAsync(cmd, ct);

            if (exit != 0 || string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException($"psql returned exit code {exit}");

            var vals = output.Trim().Split('^');
            string V(int i) => i < vals.Length ? vals[i].Trim() : "";

            long.TryParse(V(6), out var walBytes);
            int.TryParse(V(11), out var archivedCount);
            int.TryParse(V(12), out var failedCount);

            return new WalStatus(
                WalLevel: V(0).Length > 0 ? V(0) : "unknown",
                ArchiveMode: V(1).Length > 0 ? V(1) : "off",
                ArchiveCommand: V(2).Length > 0 ? V(2) : "(disabled)",
                ArchiveTimeout: V(3).Length > 0 ? V(3) : "0",
                WalSegmentSize: V(4).Length > 0 ? V(4) : "16MB",
                CurrentLsn: V(5),
                WalBytesWritten: walBytes,
                LastArchivedWal: V(7).Length > 0 ? V(7) : null,
                LastArchivedTime: V(8).Length > 0 ? V(8) : null,
                LastFailedWal: V(9).Length > 0 ? V(9) : null,
                LastFailedTime: V(10).Length > 0 ? V(10) : null,
                ArchivedCount: archivedCount,
                FailedCount: failedCount,
                IsArchivingEnabled: V(1) == "on",
                IsReplicaLevel: V(0) is "replica" or "logical"
            );
        }
        catch (OperationCanceledException)
        {
            return new WalStatus(
                "unknown", "unknown", "", "0", "16MB", "", 0,
                null, null, null, null, 0, 0, false, false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get WAL status");
            return new WalStatus(
                "unknown", "unknown", "", "0", "16MB", "", 0,
                null, null, null, null, 0, 0, false, false);
        }
    }

    public async Task<(bool Success, string Message)> EnableWalArchivingAsync(CancellationToken ct = default)
    {
        var dbUser = GetDbUser();

        try
        {
            // Create and fix ownership of archive directory
            await RunCommandAsync($"docker exec {DbContainer} sh -c \"mkdir -p /var/lib/postgresql/archive && chown postgres:postgres /var/lib/postgresql/archive\"", ct);

            // Configure all WAL settings in a single psql call
            const string alterSql = @"ALTER SYSTEM SET wal_level = 'replica';
                ALTER SYSTEM SET archive_mode = 'on';
                ALTER SYSTEM SET archive_command = 'cp %p /var/lib/postgresql/archive/%f';
                ALTER SYSTEM SET archive_timeout = '300';";
            var (exit, output) = await RunCommandAsync(
                $"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"{alterSql.Replace("\"", "\\\"")}\"", ct);
            if (exit != 0)
                return (false, $"Failed to set WAL config: {output}");

            // Check if restart is needed (single call)
            var checkCmd = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -t -A -F '|' -c \"SELECT current_setting('wal_level'), current_setting('archive_mode');\"";
            var (checkExit, checkOutput) = await RunCommandAsync(checkCmd, ct);
            var parts = checkExit == 0 ? checkOutput.Trim().Split('|') : [];
            var needsRestart = parts.Length < 2 || parts[0].Trim() != "replica" || parts[1].Trim() != "on";

            if (needsRestart)
            {
                return (true,
                    "WAL archiving configured. PostgreSQL container restart required for changes to take effect. " +
                    "Run: docker restart ivf-db");
            }

            return (true, "WAL archiving is now enabled and active.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enable WAL archiving");
            return (false, $"Error: {ex.Message}");
        }
    }

    public async Task<(string FilePath, long SizeBytes)?> CreateBaseBackupAsync(
        string outputDir,
        Action<string, string>? onLog = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);

        var dbUser = GetDbUser();

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupName = $"ivf_basebackup_{timestamp}";
        var containerBackupPath = $"/tmp/{backupName}";
        var tarFileName = $"{backupName}.tar.gz";
        var localPath = Path.Combine(outputDir, tarFileName);

        try
        {
            onLog?.Invoke("INFO", "Creating PostgreSQL base backup (pg_basebackup)...");

            // Create base backup in tar format
            var basebackupCmd = $"docker exec {DbContainer} pg_basebackup -U {dbUser} -D {containerBackupPath} -Ft -z -P --checkpoint=fast";
            var (exit, output) = await RunCommandAsync(basebackupCmd, ct);

            if (exit != 0)
            {
                onLog?.Invoke("ERROR", $"pg_basebackup failed: {output}");
                return null;
            }
            onLog?.Invoke("OK", "Base backup created successfully");

            // Tar the backup directory and copy out
            var tarCmd = $"docker exec {DbContainer} sh -c \"cd /tmp && tar czf /tmp/{tarFileName} {backupName}/\"";
            var (tarExit, tarOutput) = await RunCommandAsync(tarCmd, ct);
            if (tarExit != 0)
            {
                onLog?.Invoke("ERROR", $"Tar compression failed: {tarOutput}");
                return null;
            }

            // Copy to host
            var copyCmd = $"docker cp {DbContainer}:/tmp/{tarFileName} \"{localPath}\"";
            var (copyExit, _) = await RunCommandAsync(copyCmd, ct);
            if (copyExit != 0)
            {
                onLog?.Invoke("ERROR", "Failed to copy base backup to host");
                return null;
            }

            // Cleanup container
            await RunCommandAsync($"docker exec {DbContainer} rm -rf {containerBackupPath} /tmp/{tarFileName}", ct);

            var size = new FileInfo(localPath).Length;
            var checksum = await integrityService.ComputeAndStoreChecksumAsync(localPath, ct);
            onLog?.Invoke("OK", $"Base backup saved: {tarFileName} ({size:N0} bytes), SHA-256: {checksum}");

            return (localPath, size);
        }
        catch (Exception ex)
        {
            await RunCommandAsync($"docker exec {DbContainer} rm -rf {containerBackupPath} /tmp/{tarFileName}", CancellationToken.None);
            onLog?.Invoke("ERROR", $"Base backup failed: {ex.Message}");
            logger.LogError(ex, "Base backup failed");
            return null;
        }
    }

    public async Task<WalArchiveInfo> GetArchiveInfoAsync(CancellationToken ct = default)
    {
        try
        {
            // Single docker exec with all archive info combined
            var script = "cd /var/lib/postgresql/archive 2>/dev/null && "
                       + "echo \"$(ls -1 | wc -l)|$(du -sb . | cut -f1)|$(ls -t | head -1)\" "
                       + "|| echo '0|0|'";
            var (exit, output) = await RunCommandAsync(
                $"docker exec {DbContainer} sh -c \"{script}\"", ct);

            if (exit == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var parts = output.Trim().Split('|');
                int.TryParse(parts.ElementAtOrDefault(0)?.Trim(), out var fileCount);
                long.TryParse(parts.ElementAtOrDefault(1)?.Trim(), out var totalSize);
                var latestFile = parts.ElementAtOrDefault(2)?.Trim();
                return new WalArchiveInfo(fileCount, totalSize,
                    string.IsNullOrEmpty(latestFile) ? null : latestFile);
            }

            return new WalArchiveInfo(0, 0, null);
        }
        catch (OperationCanceledException)
        {
            // Normal when the HTTP request is cancelled or timed out — return empty gracefully
            return new WalArchiveInfo(0, 0, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get WAL archive info");
            return new WalArchiveInfo(0, 0, null);
        }
    }

    public List<BackupInfo> ListBaseBackups(string backupsDir)
    {
        if (!Directory.Exists(backupsDir))
            return [];

        return Directory.GetFiles(backupsDir, "ivf_basebackup_*.tar.gz")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new BackupInfo
            {
                FileName = f.Name,
                FullPath = f.FullName,
                SizeBytes = f.Length,
                CreatedAt = f.LastWriteTimeUtc,
                Checksum = BackupIntegrityService.LoadStoredChecksum(f.FullName)
            })
            .ToList();
    }

    public async Task<(bool Success, string Message)> SwitchWalAsync(CancellationToken ct = default)
    {
        var dbUser = GetDbUser();

        try
        {
            var result = await PsqlScalar(dbUser, "SELECT pg_switch_wal()::text;", ct);
            return (true, $"WAL switched successfully. New LSN: {result?.Trim()}");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to switch WAL: {ex.Message}");
        }
    }

    private async Task<string?> PsqlScalar(string dbUser, string sql, CancellationToken ct)
    {
        var cmd = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -t -c \"{sql.Replace("\"", "\\\"")}\"";
        var (exit, output) = await RunCommandAsync(cmd, ct);
        return exit == 0 ? output.Trim() : null;
    }

    /// <summary>
    /// Restore the database from a base backup + WAL archive to a specific point in time.
    /// Runs the restore-pitr.sh script in the background, streaming logs via onLog callback.
    /// </summary>
    public async Task RestoreFromPitrAsync(
        string baseBackupPath,
        string? targetTime,
        bool dryRun,
        Action<string, string>? onLog = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(baseBackupPath))
            throw new FileNotFoundException($"Base backup not found: {baseBackupPath}");

        onLog?.Invoke("INFO", $"Starting PITR restore from '{Path.GetFileName(baseBackupPath)}'...");

        // Build script arguments
        var args = new List<string> { $"\"{baseBackupPath}\"", "--yes" };

        if (dryRun)
            args.Add("--dry-run");

        if (!string.IsNullOrWhiteSpace(targetTime))
            args.AddRange(["--target-time", $"\"{targetTime}\""]);
        else
            args.Add("--target-latest");

        // Check for local WAL backup directory
        var backupsDir = Path.GetDirectoryName(baseBackupPath) ?? "";
        var walDir = Path.Combine(backupsDir, "wal");
        if (Directory.Exists(walDir) && Directory.GetFiles(walDir).Length > 0)
        {
            args.AddRange(["--wal-dir", $"\"{walDir}\""]);
        }

        var scriptPath = Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")),
            "scripts", "restore-pitr.sh");

        // Fallback: try relative to content root
        if (!File.Exists(scriptPath))
        {
            scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "restore-pitr.sh");
        }

        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("restore-pitr.sh script not found");

        var argString = string.Join(" ", args);

        // Use bash (Git for Windows on Windows, native on Linux)
        string bashPath;
        if (OperatingSystem.IsWindows())
        {
            bashPath = FindGitBash() ?? "bash";
        }
        else
        {
            bashPath = "/bin/bash";
        }

        var psi = new ProcessStartInfo
        {
            FileName = bashPath,
            Arguments = $"\"{scriptPath}\" {argString}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        onLog?.Invoke("INFO", $"Running: bash restore-pitr.sh {argString}");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start restore-pitr.sh");

        // Stream stdout/stderr lines in real-time
        var readStdout = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                var level = ParseLogLevel(line);
                onLog?.Invoke(level, StripAnsiCodes(line));
            }
        }, ct);

        var readStderr = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(ct) is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    onLog?.Invoke("WARN", StripAnsiCodes(line));
            }
        }, ct);

        await Task.WhenAll(readStdout, readStderr);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            onLog?.Invoke("ERROR", $"restore-pitr.sh exited with code {process.ExitCode}");
            throw new InvalidOperationException($"PITR restore failed with exit code {process.ExitCode}");
        }

        onLog?.Invoke("OK", "PITR restore completed successfully");
    }

    private static string ParseLogLevel(string line)
    {
        if (line.Contains("[OK]")) return "OK";
        if (line.Contains("[ERROR]")) return "ERROR";
        if (line.Contains("[WARN]")) return "WARN";
        return "INFO";
    }

    private static string StripAnsiCodes(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, @"\x1b\[[0-9;]*m", "");
    }

    private static string? FindGitBash()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files (x86)\Git\bin\bash.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "bin", "bash.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
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

        // Use a combined timeout (30s) + caller cancellation
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        var linked = timeoutCts.Token;

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(linked);
            var stderrTask = process.StandardError.ReadToEndAsync(linked);

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(linked);

            var stdout = stdoutTask.Result;
            var stderr = stderrTask.Result;
            var output = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
            return (process.ExitCode, output);
        }
        catch
        {
            // Kill the child process so it doesn't linger after cancellation / timeout
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }
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

public record WalStatus(
    string WalLevel,
    string ArchiveMode,
    string ArchiveCommand,
    string ArchiveTimeout,
    string WalSegmentSize,
    string CurrentLsn,
    long WalBytesWritten,
    string? LastArchivedWal,
    string? LastArchivedTime,
    string? LastFailedWal,
    string? LastFailedTime,
    int ArchivedCount,
    int FailedCount,
    bool IsArchivingEnabled,
    bool IsReplicaLevel);

public record WalArchiveInfo(int FileCount, long TotalSizeBytes, string? LatestFile);
