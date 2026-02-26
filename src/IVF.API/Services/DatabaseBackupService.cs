using System.Diagnostics;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Services;

/// <summary>
/// Service for backing up and restoring PostgreSQL database using pg_dump/pg_restore.
/// Runs inside the ivf-db Docker container.
/// Supports checksum verification and safe restore via staging database.
/// </summary>
public sealed class DatabaseBackupService(
    IConfiguration configuration,
    BackupIntegrityService integrityService,
    ILogger<DatabaseBackupService> logger)
{
    private const string StagingDbSuffix = "_restore_staging";
    private const string DbContainer = "ivf-db";

    /// <summary>
    /// Create a PostgreSQL dump (.sql.gz) using pg_dump inside the Docker container.
    /// Returns the local file path of the downloaded backup.
    /// </summary>
    public async Task<(string FilePath, long SizeBytes)> BackupDatabaseAsync(
        string outputDir,
        Action<string, string>? onLog = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var dumpFileName = $"ivf_db_{timestamp}.sql.gz";
        var containerDumpPath = $"/tmp/{dumpFileName}";
        var localPath = Path.Combine(outputDir, dumpFileName);

        // Parse connection to get DB name and user
        var connStr = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=ivf_db;Username=postgres;Password=postgres";
        var dbName = ExtractConnParam(connStr, "Database") ?? "ivf_db";
        var dbUser = ExtractConnParam(connStr, "Username") ?? "postgres";

        try
        {
            onLog?.Invoke("INFO", $"Starting PostgreSQL backup of '{dbName}'...");

            // Step 1: Run pg_dump inside Docker container, pipe through gzip
            var dumpCmd = $"docker exec {DbContainer} sh -c \"pg_dump -U {dbUser} -d {dbName} --no-owner --no-acl | gzip > {containerDumpPath}\"";
            var (exitCode, output) = await RunCommandAsync(dumpCmd, ct);

            if (exitCode != 0)
                throw new InvalidOperationException($"pg_dump failed (exit {exitCode}): {output}");

            onLog?.Invoke("OK", "pg_dump completed successfully");

            // Step 2: Copy dump from container to local
            var copyCmd = $"docker cp {DbContainer}:{containerDumpPath} \"{localPath}\"";
            var (copyExit, copyOutput) = await RunCommandAsync(copyCmd, ct);

            if (copyExit != 0)
                throw new InvalidOperationException($"docker cp failed (exit {copyExit}): {copyOutput}");

            // Step 3: Cleanup temp file in container
            await RunCommandAsync($"docker exec {DbContainer} rm -f {containerDumpPath}", ct);

            if (!File.Exists(localPath))
                throw new FileNotFoundException("Backup file was not created");

            var size = new FileInfo(localPath).Length;
            if (size < 100)
                throw new InvalidOperationException($"Backup file is suspiciously small ({size} bytes), possible empty dump");

            // Compute and store SHA-256 checksum
            var checksum = await integrityService.ComputeAndStoreChecksumAsync(localPath, ct);
            onLog?.Invoke("OK", $"SHA-256: {checksum}");

            // Verify the gzip archive is valid
            var verifyCmd = $"docker exec {DbContainer} sh -c \"cp /dev/null /tmp/_verify_test\"";
            var (_, _1) = await RunCommandAsync($"docker cp \"{localPath}\" {DbContainer}:/tmp/_verify_dump.sql.gz", ct);
            var (gzExit, gzOutput) = await RunCommandAsync($"docker exec {DbContainer} sh -c \"gunzip -t /tmp/_verify_dump.sql.gz\"", ct);
            await RunCommandAsync($"docker exec {DbContainer} rm -f /tmp/_verify_dump.sql.gz", ct);
            if (gzExit != 0)
                throw new InvalidOperationException($"Backup gzip verification failed: {gzOutput}");
            onLog?.Invoke("OK", "Backup integrity verified (gzip valid)");

            onLog?.Invoke("OK", $"Database backup saved: {dumpFileName} ({size:N0} bytes)");

            return (localPath, size);
        }
        catch (Exception ex)
        {
            onLog?.Invoke("ERROR", $"Database backup failed: {ex.Message}");
            logger.LogError(ex, "Database backup failed");
            throw;
        }
    }

    /// <summary>
    /// Restore a PostgreSQL dump (.sql.gz) using a safe staging approach:
    /// 1. Verify backup checksum
    /// 2. Restore to a staging database
    /// 3. Validate staging database (table count, data presence)
    /// 4. Swap staging → main via rename (terminates connections only at swap time)
    /// 5. If swap fails, the original database remains untouched
    /// </summary>
    public async Task RestoreDatabaseAsync(
        string backupFilePath,
        Action<string, string>? onLog = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException($"Backup file not found: {backupFilePath}");

        var connStr = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=ivf_db;Username=postgres;Password=postgres";
        var dbName = ExtractConnParam(connStr, "Database") ?? "ivf_db";
        var dbUser = ExtractConnParam(connStr, "Username") ?? "postgres";
        var stagingDb = dbName + StagingDbSuffix;
        var rollbackDb = $"{dbName}_pre_restore_{DateTime.UtcNow:yyyyMMddHHmmss}";

        var fileName = Path.GetFileName(backupFilePath);
        var containerPath = $"/tmp/{fileName}";

        try
        {
            onLog?.Invoke("INFO", $"Starting safe PostgreSQL restore from '{fileName}'...");

            // ── Step 1: Verify checksum ──
            var checksumResult = await integrityService.VerifyChecksumAsync(backupFilePath, ct);
            if (checksumResult.ExpectedChecksum != null)
            {
                if (!checksumResult.IsValid)
                    throw new InvalidOperationException($"Checksum verification failed: {checksumResult.Error}");
                onLog?.Invoke("OK", $"Checksum verified: {checksumResult.ActualChecksum}");
            }
            else
            {
                onLog?.Invoke("WARN", "No checksum file found — skipping integrity verification");
            }

            // ── Step 2: Copy backup into container ──
            var copyCmd = $"docker cp \"{backupFilePath}\" {DbContainer}:{containerPath}";
            var (copyExit, copyOutput) = await RunCommandAsync(copyCmd, ct);
            if (copyExit != 0)
                throw new InvalidOperationException($"docker cp failed: {copyOutput}");
            onLog?.Invoke("INFO", "Backup file copied to container");

            // ── Step 3: Restore to staging database ──
            onLog?.Invoke("INFO", $"Restoring to staging database '{stagingDb}'...");

            // Drop any leftover staging DB
            await RunCommandAsync($"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"DROP DATABASE IF EXISTS {stagingDb};\"", ct);

            var createCmd = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"CREATE DATABASE {stagingDb} OWNER {dbUser};\"";
            var (createExit, createOutput) = await RunCommandAsync(createCmd, ct);
            if (createExit != 0)
                throw new InvalidOperationException($"CREATE staging DATABASE failed: {createOutput}");

            var restoreCmd = $"docker exec {DbContainer} sh -c \"gunzip -c {containerPath} | psql -U {dbUser} -d {stagingDb}\"";
            var (restoreExit, restoreOutput) = await RunCommandAsync(restoreCmd, ct);

            if (restoreExit != 0 && restoreOutput.Contains("FATAL", StringComparison.OrdinalIgnoreCase))
            {
                await RunCommandAsync($"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"DROP DATABASE IF EXISTS {stagingDb};\"", ct);
                throw new InvalidOperationException($"Restore to staging failed: {restoreOutput}");
            }
            onLog?.Invoke("OK", "Restored to staging database");

            // ── Step 4: Validate staging database ──
            onLog?.Invoke("INFO", "Validating staging database...");
            var validation = await ValidateStagingDatabaseAsync(stagingDb, dbUser, ct);
            if (!validation.IsValid)
            {
                await RunCommandAsync($"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"DROP DATABASE IF EXISTS {stagingDb};\"", ct);
                throw new InvalidOperationException($"Staging database validation failed: {validation.Error}");
            }
            onLog?.Invoke("OK", $"Staging validated: {validation.TableCount} tables, {validation.RowCount} total rows");

            // ── Step 5: Atomic swap — terminate connections and rename ──
            onLog?.Invoke("WARN", "Swapping databases (brief interruption)...");

            // Terminate connections to both databases
            var terminateMain = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname IN ('{dbName}', '{stagingDb}') AND pid <> pg_backend_pid();\"";
            await RunCommandAsync(terminateMain, ct);

            // Rename main → rollback
            var renameOld = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"ALTER DATABASE {dbName} RENAME TO {rollbackDb};\"";
            var (renameOldExit, renameOldOutput) = await RunCommandAsync(renameOld, ct);

            if (renameOldExit != 0)
            {
                // Retry: connections may have reconnected — terminate again
                await RunCommandAsync(terminateMain, ct);
                await Task.Delay(500, ct);
                (renameOldExit, renameOldOutput) = await RunCommandAsync(renameOld, ct);
            }

            if (renameOldExit != 0)
            {
                onLog?.Invoke("WARN", $"Cannot rename main DB (possibly active connections), falling back to drop+create");
                // Fallback: use the original drop+create approach
                await FallbackRestoreAsync(containerPath, dbName, dbUser, stagingDb, onLog, ct);
            }
            else
            {
                // Rename staging → main
                var renameNew = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"ALTER DATABASE {stagingDb} RENAME TO {dbName};\"";
                var (renameNewExit, renameNewOutput) = await RunCommandAsync(renameNew, ct);

                if (renameNewExit != 0)
                {
                    // Rollback: rename old DB back
                    onLog?.Invoke("ERROR", "Failed to rename staging DB — rolling back");
                    await RunCommandAsync($"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"ALTER DATABASE {rollbackDb} RENAME TO {dbName};\"", ct);
                    await RunCommandAsync($"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"DROP DATABASE IF EXISTS {stagingDb};\"", ct);
                    throw new InvalidOperationException($"Rename staging→main failed: {renameNewOutput}");
                }

                onLog?.Invoke("OK", $"Database swapped (old DB preserved as '{rollbackDb}')");
            }

            // ── Step 6: Cleanup ──
            await RunCommandAsync($"docker exec {DbContainer} rm -f {containerPath}", ct);

            // Drop the rollback DB after successful swap (keep last 2 rollback DBs for safety)
            await CleanupRollbackDatabasesAsync(dbName, dbUser, maxKeep: 2, ct);

            onLog?.Invoke("OK", $"PostgreSQL restore from '{fileName}' completed safely");
        }
        catch (Exception ex)
        {
            // Always clean up container temp file
            await RunCommandAsync($"docker exec {DbContainer} rm -f {containerPath}", CancellationToken.None);
            onLog?.Invoke("ERROR", $"Database restore failed: {ex.Message}");
            logger.LogError(ex, "Database restore failed");
            throw;
        }
    }

    /// <summary>
    /// Validate a backup file without restoring. Returns validation details.
    /// </summary>
    public async Task<BackupValidationResult> ValidateBackupAsync(
        string backupFilePath,
        Action<string, string>? onLog = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath))
            return new BackupValidationResult(false, "File not found", null, 0, 0);

        var connStr = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=ivf_db;Username=postgres;Password=postgres";
        var dbUser = ExtractConnParam(connStr, "Username") ?? "postgres";

        var fileName = Path.GetFileName(backupFilePath);

        // 1. File size check
        var size = new FileInfo(backupFilePath).Length;
        if (size < 100)
            return new BackupValidationResult(false, "File is too small to be a valid backup", null, 0, 0);

        // 2. Checksum verification
        ChecksumResult? checksumResult = null;
        var checksumPath = backupFilePath + ".sha256";
        if (File.Exists(checksumPath))
        {
            checksumResult = await integrityService.VerifyChecksumAsync(backupFilePath, ct);
            if (!checksumResult.IsValid)
                return new BackupValidationResult(false, $"Checksum mismatch: {checksumResult.Error}", checksumResult.ActualChecksum, 0, 0);
            onLog?.Invoke("OK", $"Checksum verified: {checksumResult.ActualChecksum}");
        }

        // 3. Gzip integrity test
        var containerPath = $"/tmp/_validate_{fileName}";
        await RunCommandAsync($"docker cp \"{backupFilePath}\" {DbContainer}:{containerPath}", ct);
        var (gzExit, gzOutput) = await RunCommandAsync($"docker exec {DbContainer} sh -c \"gunzip -t {containerPath}\"", ct);
        if (gzExit != 0)
        {
            await RunCommandAsync($"docker exec {DbContainer} rm -f {containerPath}", ct);
            return new BackupValidationResult(false, $"Gzip integrity check failed: {gzOutput}", checksumResult?.ActualChecksum, 0, 0);
        }
        onLog?.Invoke("OK", "Gzip integrity verified");

        // 4. Restore to temp DB and validate
        var tempDb = $"ivf_validate_{DateTime.UtcNow:yyyyMMddHHmmss}";
        try
        {
            await RunCommandAsync($"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"CREATE DATABASE {tempDb} OWNER {dbUser};\"", ct);
            var (rExit, rOutput) = await RunCommandAsync($"docker exec {DbContainer} sh -c \"gunzip -c {containerPath} | psql -U {dbUser} -d {tempDb}\"", ct);

            if (rExit != 0 && rOutput.Contains("FATAL", StringComparison.OrdinalIgnoreCase))
                return new BackupValidationResult(false, $"Restore to temp DB failed: {rOutput}", checksumResult?.ActualChecksum, 0, 0);

            var validation = await ValidateStagingDatabaseAsync(tempDb, dbUser, ct);
            onLog?.Invoke("OK", $"Content validated: {validation.TableCount} tables, {validation.RowCount} rows");

            return new BackupValidationResult(true, null, checksumResult?.ActualChecksum, validation.TableCount, validation.RowCount);
        }
        finally
        {
            await RunCommandAsync($"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"DROP DATABASE IF EXISTS {tempDb};\"", ct);
            await RunCommandAsync($"docker exec {DbContainer} rm -f {containerPath}", ct);
        }
    }

    private async Task<StagingValidation> ValidateStagingDatabaseAsync(string dbName, string dbUser, CancellationToken ct)
    {
        // Check table count
        var tableCmd = $"docker exec {DbContainer} psql -U {dbUser} -d {dbName} -t -c \"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';\"";
        var (tableExit, tableOutput) = await RunCommandAsync(tableCmd, ct);
        var tableCount = 0;
        if (tableExit == 0) int.TryParse(tableOutput.Trim(), out tableCount);

        if (tableCount == 0)
            return new StagingValidation(false, "No tables found in restored database", 0, 0);

        // Check total row count across key tables
        var rowCmd = $"docker exec {DbContainer} psql -U {dbUser} -d {dbName} -t -c \"SELECT SUM(n_live_tup) FROM pg_stat_user_tables;\"";
        var (rowExit, rowOutput) = await RunCommandAsync(rowCmd, ct);
        long rowCount = 0;
        if (rowExit == 0) long.TryParse(rowOutput.Trim(), out rowCount);

        return new StagingValidation(true, null, tableCount, rowCount);
    }

    private async Task FallbackRestoreAsync(string containerPath, string dbName, string dbUser, string stagingDb, Action<string, string>? onLog, CancellationToken ct)
    {
        // Drop staging DB first
        await RunCommandAsync($"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"DROP DATABASE IF EXISTS {stagingDb};\"", ct);

        // Terminate connections
        await RunCommandAsync($"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{dbName}' AND pid <> pg_backend_pid();\"", ct);

        // Drop + recreate main
        await RunCommandAsync($"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"DROP DATABASE IF EXISTS {dbName};\"", ct);
        var (createExit, createOutput) = await RunCommandAsync($"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"CREATE DATABASE {dbName} OWNER {dbUser};\"", ct);
        if (createExit != 0)
            throw new InvalidOperationException($"CREATE DATABASE failed in fallback: {createOutput}");

        var (restoreExit, restoreOutput) = await RunCommandAsync($"docker exec {DbContainer} sh -c \"gunzip -c {containerPath} | psql -U {dbUser} -d {dbName}\"", ct);
        if (restoreExit != 0 && restoreOutput.Contains("FATAL", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Fallback restore failed: {restoreOutput}");

        onLog?.Invoke("OK", "Database restored via fallback (drop+create)");
    }

    private async Task CleanupRollbackDatabasesAsync(string dbName, string dbUser, int maxKeep, CancellationToken ct)
    {
        try
        {
            var listCmd = $"docker exec {DbContainer} psql -U {dbUser} -d postgres -t -c \"SELECT datname FROM pg_database WHERE datname LIKE '{dbName}_pre_restore_%' ORDER BY datname DESC;\"";
            var (listExit, listOutput) = await RunCommandAsync(listCmd, ct);
            if (listExit != 0) return;

            var rollbackDbs = listOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(n => n.StartsWith(dbName + "_pre_restore_"))
                .ToList();

            foreach (var oldDb in rollbackDbs.Skip(maxKeep))
            {
                await RunCommandAsync($"docker exec {DbContainer} psql -U {dbUser} -d postgres -c \"DROP DATABASE IF EXISTS {oldDb};\"", ct);
                logger.LogInformation("Cleaned up old rollback database: {DbName}", oldDb);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cleanup rollback databases");
        }
    }

    /// <summary>
    /// Get database size and table count for status reporting.
    /// </summary>
    public async Task<DatabaseInfo> GetDatabaseInfoAsync(CancellationToken ct = default)
    {
        var connStr = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=ivf_db;Username=postgres;Password=postgres";
        var dbName = ExtractConnParam(connStr, "Database") ?? "ivf_db";
        var dbUser = ExtractConnParam(connStr, "Username") ?? "postgres";

        try
        {
            // Get database size
            var sizeCmd = $"docker exec {DbContainer} psql -U {dbUser} -d {dbName} -t -c \"SELECT pg_database_size('{dbName}');\"";
            var (sizeExit, sizeOutput) = await RunCommandAsync(sizeCmd, ct);
            long dbSize = 0;
            if (sizeExit == 0 && long.TryParse(sizeOutput.Trim(), out var parsed))
                dbSize = parsed;

            // Get table count
            var tableCmd = $"docker exec {DbContainer} psql -U {dbUser} -d {dbName} -t -c \"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';\"";
            var (tableExit, tableOutput) = await RunCommandAsync(tableCmd, ct);
            int tableCount = 0;
            if (tableExit == 0 && int.TryParse(tableOutput.Trim(), out var tc))
                tableCount = tc;

            return new DatabaseInfo(dbName, dbSize, tableCount, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get database info");
            return new DatabaseInfo(dbName, 0, 0, false);
        }
    }

    /// <summary>
    /// List existing database backup files.
    /// </summary>
    public List<BackupInfo> ListDatabaseBackups(string backupsDir)
    {
        if (!Directory.Exists(backupsDir))
            return [];

        return Directory.GetFiles(backupsDir, "ivf_db_*.sql.gz")
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

        // Read stdout and stderr concurrently to avoid pipe deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        var stdout = stdoutTask.Result;
        var stderr = stderrTask.Result;
        var output = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
        return (process.ExitCode, output);
    }

    private static string? ExtractConnParam(string connStr, string key)
    {
        foreach (var part in connStr.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                return kv[1].Trim();
        }
        return null;
    }
}

public record DatabaseInfo(string DatabaseName, long SizeBytes, int TableCount, bool Connected);

public record BackupValidationResult(bool IsValid, string? Error, string? Checksum, int TableCount, long RowCount);

internal record StagingValidation(bool IsValid, string? Error, int TableCount, long RowCount);
