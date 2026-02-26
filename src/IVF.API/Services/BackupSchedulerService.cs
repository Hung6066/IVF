using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Services;

/// <summary>
/// Background service that runs scheduled backups based on a cron expression
/// and cleans up old backups according to retention policy.
/// Config and run history persisted in the database.
/// </summary>
public sealed class BackupSchedulerService : BackgroundService
{
    private readonly BackupRestoreService _backupService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupSchedulerService> _logger;
    private readonly IConfiguration _configuration;

    public DateTime? NextScheduledRun { get; private set; }

    public BackupSchedulerService(
        BackupRestoreService backupService,
        IServiceScopeFactory scopeFactory,
        ILogger<BackupSchedulerService> logger,
        IConfiguration configuration)
    {
        _backupService = backupService;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    // ─── DB-backed config ───────────────────────────────────

    public async Task<BackupScheduleConfig> GetConfigAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        var config = await db.BackupScheduleConfigs.FirstOrDefaultAsync();
        if (config != null) return config;

        // Seed from appsettings on first access
        config = BackupScheduleConfig.CreateDefault();
        var section = _configuration.GetSection("BackupSchedule");
        if (section.Exists())
        {
            config.Update(
                enabled: section.GetValue<bool?>("Enabled"),
                cronExpression: section.GetValue<string>("CronExpression"),
                keysOnly: section.GetValue<bool?>("KeysOnly"),
                retentionDays: section.GetValue<int?>("RetentionDays"),
                maxBackupCount: section.GetValue<int?>("MaxBackupCount"));
        }

        db.BackupScheduleConfigs.Add(config);
        await db.SaveChangesAsync();
        return config;
    }

    public async Task<BackupScheduleConfig> UpdateConfigAsync(
        bool? enabled, string? cronExpression, bool? keysOnly, int? retentionDays, int? maxBackupCount, bool? cloudSyncEnabled = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        var config = await db.BackupScheduleConfigs.FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Schedule config not initialized");

        config.Update(enabled, cronExpression, keysOnly, retentionDays, maxBackupCount, cloudSyncEnabled);
        await db.SaveChangesAsync();

        _logger.LogInformation("Backup schedule updated: Enabled={Enabled}, Cron={Cron}, RetentionDays={Ret}, MaxCount={Max}",
            config.Enabled, config.CronExpression, config.RetentionDays, config.MaxBackupCount);

        return config;
    }

    // ─── Scheduler loop ─────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        _logger.LogInformation("Backup scheduler started");

        // Ensure config row exists
        await GetConfigAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            BackupScheduleConfig config;
            try { config = await GetConfigAsync(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read backup schedule config");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                continue;
            }

            if (!config.Enabled)
            {
                NextScheduledRun = null;
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                continue;
            }

            var now = DateTime.UtcNow;
            var nextRun = GetNextCronTime(config.CronExpression, now);
            NextScheduledRun = nextRun;

            if (nextRun == null)
            {
                _logger.LogWarning("Invalid cron expression: {Cron}, sleeping 5 minutes", config.CronExpression);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                continue;
            }

            var delay = nextRun.Value - now;
            if (delay > TimeSpan.Zero)
            {
                _logger.LogInformation("Next scheduled backup at {NextRun} (in {Delay})", nextRun.Value, delay);

                var savedCron = config.CronExpression;
                while (delay > TimeSpan.Zero && !stoppingToken.IsCancellationRequested)
                {
                    var sleepTime = delay > TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : delay;
                    await Task.Delay(sleepTime, stoppingToken);
                    delay -= sleepTime;

                    try
                    {
                        var currentConfig = await GetConfigAsync();
                        if (!currentConfig.Enabled) { NextScheduledRun = null; break; }
                        if (currentConfig.CronExpression != savedCron) break;
                    }
                    catch { /* continue with existing schedule */ }
                }

                try { config = await GetConfigAsync(); }
                catch { continue; }
                if (!config.Enabled) continue;
            }

            try
            {
                _logger.LogInformation("Starting scheduled backup (keysOnly={KeysOnly})", config.KeysOnly);
                var operationCode = await _backupService.StartBackupAsync(config.KeysOnly, "scheduler");

                // Record in DB
                await RecordScheduledRunAsync(operationCode);

                // Wait for completion
                await WaitForOperationCompletion(operationCode, TimeSpan.FromMinutes(10), stoppingToken);

                // Auto-sync to cloud if enabled
                if (config.CloudSyncEnabled)
                {
                    try
                    {
                        var latestBackup = _backupService.ListBackups().FirstOrDefault();
                        if (latestBackup != null)
                        {
                            _logger.LogInformation("Auto-syncing backup {File} to cloud", latestBackup.FileName);
                            await _backupService.UploadToCloudAsync(latestBackup.FileName, stoppingToken);
                            _logger.LogInformation("Cloud sync completed for {File}", latestBackup.FileName);
                        }
                    }
                    catch (Exception cloudEx)
                    {
                        _logger.LogError(cloudEx, "Cloud auto-sync failed after scheduled backup");
                    }
                }

                // Cleanup old backups
                CleanupOldBackups(config);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Scheduled backup failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(61), stoppingToken);
        }
    }

    private async Task RecordScheduledRunAsync(string operationCode)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            var config = await db.BackupScheduleConfigs.FirstOrDefaultAsync();
            config?.RecordScheduledRun(operationCode);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record scheduled run in DB");
        }
    }

    private async Task WaitForOperationCompletion(string operationCode, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (!_backupService.IsRunning(operationCode))
            {
                _logger.LogInformation("Scheduled backup {OpCode} completed", operationCode);
                return;
            }
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    private void CleanupOldBackups(BackupScheduleConfig config)
    {
        try
        {
            var backups = _backupService.ListBackups();
            var toDelete = new List<string>();

            if (config.RetentionDays > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-config.RetentionDays);
                toDelete.AddRange(backups.Where(b => b.CreatedAt < cutoff).Select(b => b.FullPath));
            }

            if (config.MaxBackupCount > 0 && backups.Count > config.MaxBackupCount)
            {
                toDelete.AddRange(backups.Skip(config.MaxBackupCount).Select(b => b.FullPath));
            }

            var uniqueFiles = toDelete.Distinct().ToList();
            foreach (var file in uniqueFiles)
            {
                try
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted old backup: {File}", Path.GetFileName(file));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old backup: {File}", file);
                }
            }

            if (uniqueFiles.Count > 0)
                _logger.LogInformation("Cleaned up {Count} old backup(s)", uniqueFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during backup cleanup");
        }
    }

    // ─── Simple cron parser (5-field: min hour dom month dow) ────

    internal static DateTime? GetNextCronTime(string cron, DateTime from)
    {
        var parts = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return null;

        try
        {
            var minutes = ParseCronField(parts[0], 0, 59);
            var hours = ParseCronField(parts[1], 0, 23);
            var daysOfMonth = ParseCronField(parts[2], 1, 31);
            var months = ParseCronField(parts[3], 1, 12);
            var daysOfWeek = ParseCronField(parts[4], 0, 6);

            if (minutes == null || hours == null || daysOfMonth == null || months == null || daysOfWeek == null)
                return null;

            var candidate = from.AddMinutes(1);
            candidate = new DateTime(candidate.Year, candidate.Month, candidate.Day,
                candidate.Hour, candidate.Minute, 0, DateTimeKind.Utc);

            for (int i = 0; i < 527040; i++)
            {
                if (months.Contains(candidate.Month) &&
                    daysOfMonth.Contains(candidate.Day) &&
                    daysOfWeek.Contains((int)candidate.DayOfWeek) &&
                    hours.Contains(candidate.Hour) &&
                    minutes.Contains(candidate.Minute))
                {
                    return candidate;
                }
                candidate = candidate.AddMinutes(1);
            }
        }
        catch { /* invalid cron */ }

        return null;
    }

    private static HashSet<int>? ParseCronField(string field, int min, int max)
    {
        var result = new HashSet<int>();

        foreach (var part in field.Split(','))
        {
            var item = part.Trim();

            if (item == "*")
            {
                for (int i = min; i <= max; i++) result.Add(i);
                continue;
            }

            if (item.Contains('/'))
            {
                var stepParts = item.Split('/');
                if (stepParts.Length != 2 || !int.TryParse(stepParts[1], out var step) || step <= 0)
                    return null;

                int start = min, end = max;
                if (stepParts[0] != "*")
                {
                    if (stepParts[0].Contains('-'))
                    {
                        var rp = stepParts[0].Split('-');
                        if (rp.Length != 2 || !int.TryParse(rp[0], out start) || !int.TryParse(rp[1], out end))
                            return null;
                    }
                    else if (int.TryParse(stepParts[0], out var s))
                    {
                        start = s;
                    }
                    else return null;
                }

                for (int i = start; i <= end; i += step) result.Add(i);
                continue;
            }

            if (item.Contains('-'))
            {
                var rangeParts = item.Split('-');
                if (rangeParts.Length != 2 ||
                    !int.TryParse(rangeParts[0], out var rStart) ||
                    !int.TryParse(rangeParts[1], out var rEnd))
                    return null;

                for (int i = rStart; i <= rEnd; i++) result.Add(i);
                continue;
            }

            if (int.TryParse(item, out var val))
            {
                result.Add(val);
                continue;
            }

            return null;
        }

        return result;
    }
}
