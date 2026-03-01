using IVF.Application.Common.Interfaces;

namespace IVF.API.Endpoints;

/// <summary>
/// Security monitoring endpoints for Zero Trust dashboard.
/// Provides real-time visibility into security events, threats, and sessions.
/// Inspired by Microsoft Sentinel workbooks and AWS Security Hub dashboards.
/// All endpoints require Admin role.
/// </summary>
public static class SecurityEventEndpoints
{
    public static void MapSecurityEventEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/security")
            .WithTags("Security")
            .RequireAuthorization("AdminOnly");

        // ─── Security Events ───

        group.MapGet("/events/recent", async (
            ISecurityEventService securityEvents,
            int? count) =>
        {
            var events = await securityEvents.GetRecentEventsAsync(count ?? 50);
            return Results.Ok(events);
        }).WithName("GetRecentSecurityEvents");

        group.MapGet("/events/user/{userId:guid}", async (
            Guid userId,
            ISecurityEventService securityEvents,
            int? hours) =>
        {
            var since = DateTime.UtcNow.AddHours(-(hours ?? 24));
            var events = await securityEvents.GetEventsByUserAsync(userId, since);
            return Results.Ok(events);
        }).WithName("GetUserSecurityEvents");

        group.MapGet("/events/ip/{ipAddress}", async (
            string ipAddress,
            ISecurityEventService securityEvents,
            int? hours) =>
        {
            var since = DateTime.UtcNow.AddHours(-(hours ?? 24));
            var events = await securityEvents.GetEventsByIpAsync(ipAddress, since);
            return Results.Ok(events);
        }).WithName("GetIpSecurityEvents");

        group.MapGet("/events/high-severity", async (
            ISecurityEventService securityEvents,
            int? hours) =>
        {
            var since = DateTime.UtcNow.AddHours(-(hours ?? 24));
            var events = await securityEvents.GetHighSeverityEventsAsync(since);
            return Results.Ok(events);
        }).WithName("GetHighSeverityEvents");

        // ─── Threat Assessment ───

        group.MapPost("/assess", async (
            AssessRequestDto request,
            IThreatDetectionService threatDetection) =>
        {
            var context = new RequestSecurityContext(
                UserId: null,
                Username: request.Username,
                IpAddress: request.IpAddress,
                UserAgent: request.UserAgent,
                DeviceFingerprint: null,
                Country: request.Country,
                City: null,
                RequestPath: request.RequestPath ?? "/",
                RequestMethod: "GET",
                SessionId: null,
                CorrelationId: Guid.NewGuid().ToString(),
                Timestamp: DateTime.UtcNow);

            var assessment = await threatDetection.AssessRequestAsync(context);
            return Results.Ok(assessment);
        }).WithName("AssessThreat");

        // ─── Active Sessions ───

        group.MapGet("/sessions/{userId:guid}", async (
            Guid userId,
            IAdaptiveSessionService sessionService) =>
        {
            var sessions = await sessionService.GetActiveSessionsAsync(userId);
            return Results.Ok(sessions);
        }).WithName("GetActiveSessions");

        group.MapDelete("/sessions/{sessionId}", async (
            string sessionId,
            IAdaptiveSessionService sessionService) =>
        {
            await sessionService.RevokeSessionAsync(sessionId, "Admin revocation");
            return Results.Ok(new { message = "Session revoked" });
        }).WithName("RevokeSession");

        // ─── IP Intelligence ───

        group.MapGet("/ip-intelligence/{ipAddress}", async (
            string ipAddress,
            IThreatDetectionService threatDetection) =>
        {
            var result = await threatDetection.CheckIpReputationAsync(ipAddress);
            return Results.Ok(result);
        }).WithName("CheckIpIntelligence");

        // ─── Device Trust ───

        group.MapGet("/device-trust/{userId:guid}/{fingerprint}", async (
            Guid userId,
            string fingerprint,
            IDeviceFingerprintService deviceFingerprintService) =>
        {
            var result = await deviceFingerprintService.CheckDeviceTrustAsync(userId, fingerprint);
            return Results.Ok(result);
        }).WithName("CheckDeviceTrust");

        // ─── Zero Trust Dashboard Summary ───

        group.MapGet("/dashboard", async (
            ISecurityEventService securityEvents) =>
        {
            var since24h = DateTime.UtcNow.AddHours(-24);
            var highSeverity = await securityEvents.GetHighSeverityEventsAsync(since24h);
            var recent = await securityEvents.GetRecentEventsAsync(10);

            var blockedCount = highSeverity.Count(e => e.IsBlocked);
            var threatsByType = highSeverity
                .GroupBy(e => e.EventType)
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count);

            return Results.Ok(new
            {
                last24Hours = new
                {
                    totalHighSeverity = highSeverity.Count,
                    blockedRequests = blockedCount,
                    threatsByType,
                    uniqueIps = highSeverity.Select(e => e.IpAddress).Distinct().Count(),
                    uniqueUsers = highSeverity.Where(e => e.UserId.HasValue).Select(e => e.UserId).Distinct().Count()
                },
                recentEvents = recent
            });
        }).WithName("GetSecurityDashboard");
    }
}

// ─── DTOs ───

public record AssessRequestDto(
    string IpAddress,
    string? Username,
    string? UserAgent,
    string? Country,
    string? RequestPath
);
