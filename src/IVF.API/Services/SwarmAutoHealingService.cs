using Microsoft.AspNetCore.SignalR;
using IVF.API.Hubs;

namespace IVF.API.Services;

/// <summary>
/// Background service that monitors Docker Swarm health and automatically remediates:
/// - Detects node failures → re-balances services to healthy nodes
/// - Detects crashed services → force-restarts after backoff
/// - Detects stuck deployments → auto-rollback after timeout
/// - Sends events to SignalR + Discord on remediation actions
/// </summary>
public sealed class SwarmAutoHealingService(
    InfrastructureMonitorService monitorService,
    IHubContext<InfrastructureHub> hubContext,
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<SwarmAutoHealingService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ForceRestartCooldown = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StuckUpdateTimeout = TimeSpan.FromMinutes(10);

    // Track remediation history to avoid loops
    private readonly Dictionary<string, DateTime> _lastForceRestart = new();
    private readonly Dictionary<string, int> _failureCount = new();
    private readonly List<HealingEventDto> _recentEvents = new();
    private readonly Lock _lock = new();

    private bool Enabled => configuration.GetValue("AutoHealing:Enabled", true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Swarm auto-healing service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (Enabled)
            {
                try
                {
                    await RunHealingCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Auto-healing cycle error");
                }
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }

        logger.LogInformation("Swarm auto-healing service stopped");
    }

    private async Task RunHealingCycleAsync(CancellationToken ct)
    {
        // 1. Check for down nodes
        var nodes = await monitorService.GetSwarmNodesAsync(ct);
        foreach (var node in nodes)
        {
            if (node.Status == "Down" && node.Availability != "Drain")
            {
                await HandleNodeFailureAsync(node, ct);
            }
        }

        // 2. Check for failing services
        var services = await monitorService.GetSwarmServicesAsync(ct);
        foreach (var svc in services)
        {
            if (svc.Status == "down" && svc.DesiredReplicas > 0)
            {
                await HandleServiceDownAsync(svc, ct);
            }
            else if (svc.Status == "degraded")
            {
                await HandleServiceDegradedAsync(svc, ct);
            }
        }

        // 3. Check for stuck deployments
        foreach (var svc in services)
        {
            await CheckStuckUpdateAsync(svc, ct);
        }
    }

    private async Task HandleNodeFailureAsync(SwarmNodeDto node, CancellationToken ct)
    {
        var eventMsg = $"Node {node.Hostname} ({node.Id}) đã Down — tự động drain để migrate tasks";
        logger.LogWarning(eventMsg);

        // Drain the failed node so Swarm reschedules tasks
        var result = await monitorService.SetNodeAvailabilityAsync(node.Id, "drain", ct);

        var healEvent = new HealingEventDto(
            Timestamp: DateTime.UtcNow,
            Type: "node_failure",
            Target: node.Hostname,
            Action: "auto_drain",
            Result: result.Success ? "success" : "failed",
            Message: result.Success ? eventMsg : $"Drain failed: {result.Message}");

        await RecordAndBroadcastAsync(healEvent, ct);
    }

    private async Task HandleServiceDownAsync(SwarmServiceDto svc, CancellationToken ct)
    {
        // Check cooldown
        lock (_lock)
        {
            if (_lastForceRestart.TryGetValue(svc.Name, out var lastRestart) &&
                DateTime.UtcNow - lastRestart < ForceRestartCooldown)
            {
                return; // Still in cooldown
            }

            // Track failure count for escalation
            _failureCount.TryGetValue(svc.Name, out var count);
            _failureCount[svc.Name] = count + 1;
        }

        var eventMsg = $"Service {svc.Name} hoàn toàn down (0/{svc.DesiredReplicas}) — force restart";
        logger.LogWarning(eventMsg);

        var result = await monitorService.ForceUpdateServiceAsync(svc.Name, ct);

        lock (_lock)
        {
            _lastForceRestart[svc.Name] = DateTime.UtcNow;
        }

        var healEvent = new HealingEventDto(
            Timestamp: DateTime.UtcNow,
            Type: "service_down",
            Target: svc.Name,
            Action: "force_restart",
            Result: result.Success ? "success" : "failed",
            Message: result.Success ? eventMsg : $"Force restart failed: {result.Message}");

        await RecordAndBroadcastAsync(healEvent, ct);
    }

    private async Task HandleServiceDegradedAsync(SwarmServiceDto svc, CancellationToken ct)
    {
        // Only act if degraded for multiple cycles (tracked by failure count)
        int count;
        lock (_lock)
        {
            _failureCount.TryGetValue(svc.Name, out count);
            _failureCount[svc.Name] = count + 1;
        }

        // Wait for 3 consecutive degraded checks (~90s) before acting
        if (count < 3) return;

        // Check cooldown
        lock (_lock)
        {
            if (_lastForceRestart.TryGetValue(svc.Name, out var lastRestart) &&
                DateTime.UtcNow - lastRestart < ForceRestartCooldown)
                return;
        }

        var eventMsg = $"Service {svc.Name} degraded ({svc.RunningReplicas}/{svc.DesiredReplicas}) — force restart after {count} checks";
        logger.LogWarning(eventMsg);

        var result = await monitorService.ForceUpdateServiceAsync(svc.Name, ct);

        lock (_lock)
        {
            _lastForceRestart[svc.Name] = DateTime.UtcNow;
            _failureCount[svc.Name] = 0; // Reset counter
        }

        var healEvent = new HealingEventDto(
            Timestamp: DateTime.UtcNow,
            Type: "service_degraded",
            Target: svc.Name,
            Action: "force_restart",
            Result: result.Success ? "success" : "failed",
            Message: eventMsg);

        await RecordAndBroadcastAsync(healEvent, ct);
    }

    private async Task CheckStuckUpdateAsync(SwarmServiceDto svc, CancellationToken ct)
    {
        var inspect = await monitorService.InspectServiceAsync(svc.Name, ct);
        if (inspect is null) return;

        // If update is in progress and older than timeout → auto rollback
        if (inspect.UpdateState is "updating" or "paused")
        {
            if (DateTime.TryParse(inspect.UpdatedAt, out var updatedAt) &&
                DateTime.UtcNow - updatedAt > StuckUpdateTimeout)
            {
                var eventMsg = $"Service {svc.Name} update stuck ({inspect.UpdateState}) > {StuckUpdateTimeout.TotalMinutes}m — auto rollback";
                logger.LogWarning(eventMsg);

                var result = await monitorService.RollbackServiceAsync(svc.Name, ct);

                var healEvent = new HealingEventDto(
                    Timestamp: DateTime.UtcNow,
                    Type: "stuck_update",
                    Target: svc.Name,
                    Action: "auto_rollback",
                    Result: result.Success ? "success" : "failed",
                    Message: eventMsg);

                await RecordAndBroadcastAsync(healEvent, ct);
            }
        }
    }

    private async Task RecordAndBroadcastAsync(HealingEventDto healEvent, CancellationToken ct)
    {
        lock (_lock)
        {
            _recentEvents.Add(healEvent);
            // Keep last 100 events
            if (_recentEvents.Count > 100)
                _recentEvents.RemoveRange(0, _recentEvents.Count - 100);
        }

        // Push to SignalR
        await hubContext.Clients.Group("infra-monitoring")
            .SendAsync("HealingEvent", healEvent, ct);

        // Push to Discord
        try
        {
            using var scope = serviceProvider.CreateScope();
            var discord = scope.ServiceProvider.GetRequiredService<DiscordAlertService>();
            var level = healEvent.Type == "node_failure" ? "critical" : "warning";
            await discord.SendAlertAsync(
                $"Auto-Heal: {healEvent.Target}",
                $"[{healEvent.Action}] {healEvent.Message} → {healEvent.Result}",
                level, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send healing event to Discord");
        }
    }

    /// <summary>Get recent healing events for display in the UI.</summary>
    public List<HealingEventDto> GetRecentEvents()
    {
        lock (_lock)
        {
            return [.. _recentEvents];
        }
    }
}

// ─── Healing DTOs ───────────────────────────────────
public sealed record HealingEventDto(
    DateTime Timestamp,
    string Type,     // node_failure, service_down, service_degraded, stuck_update
    string Target,   // node hostname or service name
    string Action,   // auto_drain, force_restart, auto_rollback
    string Result,   // success, failed
    string Message);
