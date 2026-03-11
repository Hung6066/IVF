using IVF.API.Services;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class InfrastructureEndpoints
{
    public static void MapInfrastructureEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/infrastructure")
            .WithTags("Infrastructure Monitoring")
            .RequireAuthorization("AdminOnly");

        // ═══ VPS Metrics ═══
        group.MapGet("/metrics", async (InfrastructureMonitorService svc, CancellationToken ct) =>
        {
            var metrics = await svc.GetVpsMetricsAsync(ct);
            return Results.Ok(metrics);
        }).WithName("GetVpsMetrics");

        // ═══ Swarm Services ═══
        group.MapGet("/swarm/services", async (InfrastructureMonitorService svc, CancellationToken ct) =>
        {
            var services = await svc.GetSwarmServicesAsync(ct);
            return Results.Ok(services);
        }).WithName("GetSwarmServices");

        group.MapGet("/swarm/nodes", async (InfrastructureMonitorService svc, CancellationToken ct) =>
        {
            var nodes = await svc.GetSwarmNodesAsync(ct);
            return Results.Ok(nodes);
        }).WithName("GetSwarmNodes");

        group.MapPost("/swarm/scale", async (InfrastructureMonitorService svc, ScaleServiceRequest req, CancellationToken ct) =>
        {
            var result = await svc.ScaleServiceAsync(req.ServiceName, req.Replicas, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("ScaleSwarmService");

        // ═══ Node Management ═══
        group.MapPost("/swarm/nodes/availability", async (InfrastructureMonitorService svc, NodeAvailabilityRequest req, CancellationToken ct) =>
        {
            var result = await svc.SetNodeAvailabilityAsync(req.NodeId, req.Availability, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("SetNodeAvailability");

        group.MapPost("/swarm/nodes/promote", async (InfrastructureMonitorService svc, ServiceNameRequest req, CancellationToken ct) =>
        {
            var result = await svc.PromoteNodeAsync(req.ServiceName, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("PromoteNode");

        group.MapPost("/swarm/nodes/demote", async (InfrastructureMonitorService svc, ServiceNameRequest req, CancellationToken ct) =>
        {
            var result = await svc.DemoteNodeAsync(req.ServiceName, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("DemoteNode");

        group.MapPost("/swarm/nodes/remove", async (InfrastructureMonitorService svc, ServiceNameRequest req, bool force, CancellationToken ct) =>
        {
            var result = await svc.RemoveNodeAsync(req.ServiceName, force, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("RemoveNode");

        group.MapPost("/swarm/nodes/label", async (InfrastructureMonitorService svc, NodeLabelRequest req, CancellationToken ct) =>
        {
            var result = await svc.SetNodeLabelAsync(req.NodeId, req.Key, req.Value, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("SetNodeLabel");

        group.MapDelete("/swarm/nodes/{nodeId}/label/{key}", async (InfrastructureMonitorService svc, string nodeId, string key, CancellationToken ct) =>
        {
            var result = await svc.RemoveNodeLabelAsync(nodeId, key, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("RemoveNodeLabel");

        // ═══ Service Operations ═══
        group.MapPost("/swarm/services/update-image", async (InfrastructureMonitorService svc, ServiceUpdateImageRequest req, CancellationToken ct) =>
        {
            var result = await svc.UpdateServiceImageAsync(req.ServiceName, req.NewImage, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("UpdateServiceImage");

        group.MapPost("/swarm/services/rollback", async (InfrastructureMonitorService svc, ServiceNameRequest req, CancellationToken ct) =>
        {
            var result = await svc.RollbackServiceAsync(req.ServiceName, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("RollbackService");

        group.MapPost("/swarm/services/force-update", async (InfrastructureMonitorService svc, ServiceNameRequest req, CancellationToken ct) =>
        {
            var result = await svc.ForceUpdateServiceAsync(req.ServiceName, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("ForceUpdateService");

        group.MapGet("/swarm/services/{serviceName}/tasks", async (InfrastructureMonitorService svc, string serviceName, CancellationToken ct) =>
        {
            var tasks = await svc.GetServiceTasksAsync(serviceName, ct);
            return Results.Ok(tasks);
        }).WithName("GetServiceTasks");

        group.MapGet("/swarm/services/{serviceName}/logs", async (InfrastructureMonitorService svc, string serviceName, int? tail, CancellationToken ct) =>
        {
            var logs = await svc.GetServiceLogsAsync(serviceName, tail ?? 100, ct);
            return Results.Ok(logs);
        }).WithName("GetServiceLogs");

        group.MapGet("/swarm/services/{serviceName}/inspect", async (InfrastructureMonitorService svc, string serviceName, CancellationToken ct) =>
        {
            var info = await svc.InspectServiceAsync(serviceName, ct);
            return info is not null ? Results.Ok(info) : Results.NotFound();
        }).WithName("InspectService");

        // ═══ Swarm Events ═══
        group.MapGet("/swarm/events", async (InfrastructureMonitorService svc, int? sinceMinutes, CancellationToken ct) =>
        {
            var events = await svc.GetRecentEventsAsync(sinceMinutes ?? 15, ct);
            return Results.Ok(events);
        }).WithName("GetSwarmEvents");

        // ═══ Auto-Healing ═══
        group.MapGet("/healing/events", (SwarmAutoHealingService healingSvc) =>
        {
            return Results.Ok(healingSvc.GetRecentEvents());
        }).WithName("GetHealingEvents");

        // ═══ Health Checks ═══
        group.MapGet("/health", async (InfrastructureMonitorService svc, CancellationToken ct) =>
        {
            var health = await svc.GetHealthStatusAsync(ct);
            return Results.Ok(health);
        }).WithName("GetInfraHealth");

        // ═══ Alerts ═══
        group.MapGet("/alerts", async (InfrastructureMonitorService svc, CancellationToken ct) =>
        {
            var alerts = await svc.EvaluateAlertsAsync(ct);
            return Results.Ok(alerts);
        }).WithName("GetInfraAlerts");

        // ═══ S3 Backup Status ═══
        group.MapGet("/s3/status", async (InfrastructureMonitorService svc, CancellationToken ct) =>
        {
            var status = await svc.GetS3StatusAsync(ct);
            return Results.Ok(status);
        }).WithName("GetS3Status");

        group.MapGet("/s3/objects", async (InfrastructureMonitorService svc, string? prefix, CancellationToken ct) =>
        {
            var objects = await svc.ListS3BackupsAsync(prefix, ct);
            return Results.Ok(objects);
        }).WithName("ListS3Objects");

        group.MapPost("/s3/upload", async (InfrastructureMonitorService svc, S3UploadRequest req, CancellationToken ct) =>
        {
            var result = await svc.UploadBackupToS3Async(req.FileName, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("UploadToS3");

        group.MapPost("/s3/download", async (InfrastructureMonitorService svc, S3DownloadRequest req, CancellationToken ct) =>
        {
            var result = await svc.DownloadFromS3Async(req.ObjectKey, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).WithName("DownloadFromS3");

        group.MapDelete("/s3/objects/{*objectKey}", async (InfrastructureMonitorService svc, string objectKey, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteS3ObjectAsync(objectKey, ct);
            return deleted ? Results.Ok(new { message = "Đã xoá" }) : Results.NotFound(new { error = "Không tìm thấy object" });
        }).WithName("DeleteS3Object");

        // ═══ Data Retention Policies ═══
        group.MapGet("/retention/policies", async (IvfDbContext db, CancellationToken ct) =>
        {
            var policies = await db.Set<DataRetentionPolicy>()
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.EntityType)
                .Select(p => new RetentionPolicyDto(
                    p.Id, p.EntityType, p.RetentionDays, p.Action,
                    p.IsEnabled, p.LastExecutedAt, p.LastPurgedCount))
                .ToListAsync(ct);
            return Results.Ok(policies);
        }).WithName("GetRetentionPolicies");

        group.MapPost("/retention/execute", async (IDataRetentionService svc, CancellationToken ct) =>
        {
            var result = await svc.ExecutePoliciesAsync(ct);
            return Results.Ok(result);
        }).WithName("ExecuteRetentionPolicies");

        // ═══ Read Replica Status ═══
        group.MapGet("/replica/status", async (IvfDbContext db, CancellationToken ct) =>
        {
            try
            {
                var isReplica = await db.Database.SqlQueryRaw<bool>(
                    "SELECT pg_is_in_recovery() AS \"Value\"").FirstOrDefaultAsync(ct);
                var replicationSlots = await db.Database.SqlQueryRaw<int>(
                    "SELECT COALESCE(count(*)::int, 0) AS \"Value\" FROM pg_replication_slots").FirstOrDefaultAsync(ct);
                var walReceivers = await db.Database.SqlQueryRaw<int>(
                    "SELECT COALESCE(count(*)::int, 0) AS \"Value\" FROM pg_stat_replication").FirstOrDefaultAsync(ct);

                return Results.Ok(new ReplicaStatusDto(
                    IsReplica: isReplica,
                    ActiveReplicationSlots: replicationSlots,
                    StreamingReplicas: walReceivers,
                    ConnectionString: "primary"));
            }
            catch
            {
                return Results.Ok(new ReplicaStatusDto(false, 0, 0, "unavailable"));
            }
        }).WithName("GetReplicaStatus");

        // ═══ Monitoring Stack Status ═══
        group.MapGet("/monitoring/status", async (IHttpClientFactory httpFactory, IConfiguration config, CancellationToken ct) =>
        {
            var checks = new List<MonitoringServiceStatus>();

            async Task CheckService(string name, string url, string? basicAuth = null)
            {
                try
                {
                    using var client = httpFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(3);
                    if (basicAuth is not null)
                        client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicAuth);
                    var response = await client.GetAsync(url, ct);
                    checks.Add(new MonitoringServiceStatus(name, response.IsSuccessStatusCode, (int)response.StatusCode));
                }
                catch
                {
                    checks.Add(new MonitoringServiceStatus(name, false, 0));
                }
            }

            // Prometheus basic auth: read from configuration (not hardcoded)
            var prometheusAuth = config["Monitoring:PrometheusAuth"] ?? string.Empty;

            await Task.WhenAll(
                CheckService("Prometheus", "http://prometheus:9090/-/healthy", prometheusAuth),
                CheckService("Grafana", "http://grafana:3000/api/health"),
                CheckService("Loki", "http://loki:3100/ready")
            );

            return Results.Ok(new MonitoringStackStatus(checks, DateTime.UtcNow));
        }).WithName("GetMonitoringStatus");
    }
}

// Request DTOs
public record ScaleServiceRequest(string ServiceName, int Replicas);
public record S3UploadRequest(string FileName);
public record S3DownloadRequest(string ObjectKey);

// Response DTOs
public record RetentionPolicyDto(
    Guid Id, string EntityType, int RetentionDays, string Action,
    bool IsEnabled, DateTime? LastExecutedAt, int? LastPurgedCount);
public record ReplicaStatusDto(
    bool IsReplica, int ActiveReplicationSlots, int StreamingReplicas, string ConnectionString);
public record MonitoringServiceStatus(string Name, bool Healthy, int StatusCode);
public record MonitoringStackStatus(List<MonitoringServiceStatus> Services, DateTime CheckedAt);
