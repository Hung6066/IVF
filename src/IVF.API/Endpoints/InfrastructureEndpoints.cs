using IVF.API.Services;

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
    }
}

// Request DTOs
public record ScaleServiceRequest(string ServiceName, int Replicas);
public record S3UploadRequest(string FileName);
public record S3DownloadRequest(string ObjectKey);
