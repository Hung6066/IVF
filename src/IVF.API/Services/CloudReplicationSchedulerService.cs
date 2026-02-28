namespace IVF.API.Services;

/// <summary>
/// Background service that runs MinIO cloud replication on a cron schedule.
/// Syncs local MinIO buckets to the configured remote S3-compatible target.
/// </summary>
public sealed class CloudReplicationSchedulerService(
    CloudReplicationService replicationService,
    ILogger<CloudReplicationSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        logger.LogInformation("Cloud replication scheduler started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var config = await replicationService.GetConfigAsync(stoppingToken);

                if (!config.MinioReplicationEnabled || string.IsNullOrWhiteSpace(config.RemoteMinioSyncCron))
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                var now = DateTime.UtcNow;
                var next = BackupSchedulerService.GetNextCronTime(config.RemoteMinioSyncCron, now);

                if (next == null)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                var delay = next.Value - now;
                if (delay > TimeSpan.Zero)
                {
                    logger.LogDebug("Next MinIO sync at {Next} (in {Delay})", next, delay);
                    await Task.Delay(delay, stoppingToken);
                }

                // Re-check if still enabled after waiting
                config = await replicationService.GetConfigAsync(stoppingToken);
                if (!config.MinioReplicationEnabled)
                    continue;

                logger.LogInformation("Starting scheduled MinIO cloud sync");
                var result = await replicationService.SyncMinioAsync(stoppingToken);

                if (result.Success)
                    logger.LogInformation("MinIO cloud sync completed: {Files} files", result.TotalFiles);
                else
                    logger.LogWarning("MinIO cloud sync failed: {Message}", result.Message);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cloud replication scheduler error");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
