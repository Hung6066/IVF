using Microsoft.AspNetCore.SignalR;
using IVF.API.Hubs;

namespace IVF.API.Services;

/// <summary>
/// Background service that pushes VPS metrics, Swarm status, health checks, and alerts
/// to connected admin clients via SignalR every 5 seconds.
/// </summary>
public sealed class InfrastructureMetricsPusher(
    IHubContext<InfrastructureHub> hubContext,
    InfrastructureMonitorService monitorService,
    IServiceProvider serviceProvider,
    ILogger<InfrastructureMetricsPusher> logger) : BackgroundService
{
    private static readonly TimeSpan MetricsInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HealthInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AlertInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Infrastructure metrics pusher started");

        var lastHealthCheck = DateTime.MinValue;
        var lastAlertCheck = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Always push VPS metrics (CPU/RAM/disk) — every 5s
                var metrics = await monitorService.GetVpsMetricsAsync(stoppingToken);
                await hubContext.Clients.Group("infra-monitoring")
                    .SendAsync("VpsMetrics", metrics, stoppingToken);

                // Push Swarm services status — every 5s
                var swarmServices = await monitorService.GetSwarmServicesAsync(stoppingToken);
                await hubContext.Clients.Group("infra-monitoring")
                    .SendAsync("SwarmServices", swarmServices, stoppingToken);

                // Push Swarm nodes status — every 5s
                var swarmNodes = await monitorService.GetSwarmNodesAsync(stoppingToken);
                await hubContext.Clients.Group("infra-monitoring")
                    .SendAsync("SwarmNodes", swarmNodes, stoppingToken);

                // Health checks — every 30s (more expensive)
                if (now - lastHealthCheck >= HealthInterval)
                {
                    var health = await monitorService.GetHealthStatusAsync(stoppingToken);
                    await hubContext.Clients.Group("infra-monitoring")
                        .SendAsync("HealthStatus", health, stoppingToken);
                    lastHealthCheck = now;
                }

                // Alerts — every 15s
                if (now - lastAlertCheck >= AlertInterval)
                {
                    var alerts = await monitorService.EvaluateAlertsAsync(stoppingToken);
                    if (alerts.Count > 0)
                    {
                        await hubContext.Clients.Group("infra-monitoring")
                            .SendAsync("Alerts", alerts, stoppingToken);

                        // Forward alerts to Discord
                        using var scope = serviceProvider.CreateScope();
                        var discordService = scope.ServiceProvider.GetRequiredService<DiscordAlertService>();
                        await discordService.SendAlertsAsync(alerts, stoppingToken);
                    }
                    lastAlertCheck = now;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error pushing infrastructure metrics");
            }

            await Task.Delay(MetricsInterval, stoppingToken);
        }

        logger.LogInformation("Infrastructure metrics pusher stopped");
    }
}
