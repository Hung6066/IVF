using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Services;

/// <summary>
/// Background service that executes data backup strategies on their cron schedules.
/// Each strategy defines what to back up (DB/MinIO), when, and retention rules.
/// </summary>
public sealed class DataBackupSchedulerService : BackgroundService
{
    private readonly DataBackupService _dataBackupService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataBackupSchedulerService> _logger;
    private readonly IWebHostEnvironment _env;

    public DataBackupSchedulerService(
        DataBackupService dataBackupService,
        IServiceScopeFactory scopeFactory,
        ILogger<DataBackupSchedulerService> logger,
        IWebHostEnvironment env)
    {
        _dataBackupService = dataBackupService;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _env = env;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app start up before scheduling
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        _logger.LogInformation("Data backup scheduler started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var strategies = await GetEnabledStrategiesAsync();

                if (strategies.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                // Find the next strategy that needs to run
                var now = DateTime.UtcNow;
                DataBackupStrategy? nextStrategy = null;
                DateTime? earliestRun = null;

                foreach (var strategy in strategies)
                {
                    var nextRun = BackupSchedulerService.GetNextCronTime(strategy.CronExpression, strategy.LastRunAt ?? now.AddYears(-1));
                    if (nextRun == null) continue;

                    if (nextRun <= now)
                    {
                        // This strategy is due — run it immediately
                        nextStrategy = strategy;
                        earliestRun = nextRun;
                        break;
                    }

                    if (earliestRun == null || nextRun < earliestRun)
                    {
                        nextStrategy = strategy;
                        earliestRun = nextRun;
                    }
                }

                if (nextStrategy == null || earliestRun == null)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                // Wait until the next run time
                var delay = earliestRun.Value - now;
                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation(
                        "Next data backup strategy '{Name}' scheduled at {NextRun} (in {Delay})",
                        nextStrategy.Name, earliestRun.Value, delay);

                    // Sleep in 1-minute increments to check for config changes
                    while (delay > TimeSpan.Zero && !stoppingToken.IsCancellationRequested)
                    {
                        var sleepTime = delay > TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : delay;
                        await Task.Delay(sleepTime, stoppingToken);
                        delay -= sleepTime;
                    }

                    // Re-fetch to check if still enabled
                    var refreshed = await GetStrategyByIdAsync(nextStrategy.Id);
                    if (refreshed == null || !refreshed.Enabled) continue;
                    nextStrategy = refreshed;
                }

                // Execute the strategy
                await ExecuteStrategyAsync(nextStrategy, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data backup scheduler error");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task ExecuteStrategyAsync(DataBackupStrategy strategy, CancellationToken ct)
    {
        _logger.LogInformation(
            "Executing data backup strategy '{Name}' (DB={IncludeDb}, MinIO={IncludeMinio}, Cloud={Cloud})",
            strategy.Name, strategy.IncludeDatabase, strategy.IncludeMinio, strategy.UploadToCloud);

        try
        {
            var operationCode = await _dataBackupService.StartDataBackupAsync(
                strategy.IncludeDatabase,
                strategy.IncludeMinio,
                strategy.UploadToCloud,
                $"scheduler:{strategy.Name}",
                ct);

            // Wait for operation to complete (max 30 minutes)
            await WaitForOperationCompletion(operationCode, TimeSpan.FromMinutes(30), ct);

            // Determine final status
            var status = await GetOperationStatusAsync(operationCode);

            // Record run result
            await RecordStrategyRunAsync(strategy.Id, operationCode, status ?? "Unknown");

            // Run retention cleanup
            CleanupOldBackups(strategy);

            _logger.LogInformation(
                "Strategy '{Name}' completed with status: {Status}", strategy.Name, status);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Strategy '{Name}' execution failed", strategy.Name);
            await RecordStrategyRunAsync(strategy.Id, null, "Failed");
        }
    }

    private async Task WaitForOperationCompletion(string operationCode, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var logs = _dataBackupService.GetLiveLogs(operationCode);
            if (logs == null)
                return; // Operation is no longer tracked → completed
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    private async Task<string?> GetOperationStatusAsync(string operationCode)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            var op = await db.BackupOperations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OperationCode == operationCode);
            return op?.Status.ToString();
        }
        catch { return null; }
    }

    private async Task RecordStrategyRunAsync(Guid strategyId, string? operationCode, string status)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            var strategy = await db.DataBackupStrategies.FindAsync(strategyId);
            if (strategy != null)
            {
                strategy.RecordRun(operationCode ?? "N/A", status);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record strategy run for {Id}", strategyId);
        }
    }

    private void CleanupOldBackups(DataBackupStrategy strategy)
    {
        try
        {
            var backupsDir = Path.Combine(
                Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "..")),
                "backups");

            if (!Directory.Exists(backupsDir)) return;

            var patterns = new List<string>();
            if (strategy.IncludeDatabase) patterns.Add("ivf_db_*.sql.gz");
            if (strategy.IncludeMinio) patterns.Add("ivf_minio_*.tar.gz");

            var allFiles = patterns
                .SelectMany(p => Directory.GetFiles(backupsDir, p))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTimeUtc)
                .ToList();

            var toDelete = new List<string>();

            // Retention by age
            if (strategy.RetentionDays > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-strategy.RetentionDays);
                toDelete.AddRange(allFiles.Where(f => f.CreationTimeUtc < cutoff).Select(f => f.FullName));
            }

            // Retention by count (per type)
            if (strategy.MaxBackupCount > 0)
            {
                if (strategy.IncludeDatabase)
                {
                    var dbFiles = allFiles.Where(f => f.Name.StartsWith("ivf_db_")).ToList();
                    if (dbFiles.Count > strategy.MaxBackupCount)
                        toDelete.AddRange(dbFiles.Skip(strategy.MaxBackupCount).Select(f => f.FullName));
                }
                if (strategy.IncludeMinio)
                {
                    var minioFiles = allFiles.Where(f => f.Name.StartsWith("ivf_minio_")).ToList();
                    if (minioFiles.Count > strategy.MaxBackupCount)
                        toDelete.AddRange(minioFiles.Skip(strategy.MaxBackupCount).Select(f => f.FullName));
                }
            }

            var uniqueFiles = toDelete.Distinct().ToList();
            foreach (var file in uniqueFiles)
            {
                try
                {
                    File.Delete(file);
                    _logger.LogInformation("Strategy '{Strategy}' cleanup: deleted {File}", strategy.Name, Path.GetFileName(file));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old backup: {File}", file);
                }
            }

            if (uniqueFiles.Count > 0)
                _logger.LogInformation("Strategy '{Strategy}' cleaned up {Count} old backup(s)", strategy.Name, uniqueFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cleanup failed for strategy '{Strategy}'", strategy.Name);
        }
    }

    // ─── DB access helpers ──────────────────────────────

    private async Task<List<DataBackupStrategy>> GetEnabledStrategiesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        return await db.DataBackupStrategies
            .Where(s => s.Enabled)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
    }

    private async Task<DataBackupStrategy?> GetStrategyByIdAsync(Guid id)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        return await db.DataBackupStrategies.FindAsync(id);
    }
}
