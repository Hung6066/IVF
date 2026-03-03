using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using IVF.API.Services;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace IVF.API.Endpoints;

public static class EnterpriseSecurityEndpoints
{
    public static void MapEnterpriseSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/security/enterprise")
            .WithTags("EnterpriseSecurity")
            .RequireAuthorization("AdminOnly");

        MapConditionalAccessPolicies(group);
        MapIncidentResponse(group);
        MapDataRetention(group);
        MapImpersonation(group);
        MapPermissionDelegation(group);
        MapBehavioralAnalytics(group);
        MapSecurityIncidents(group);
        MapNotificationPreferences(group);
    }

    // ─── Conditional Access Policies ───

    private static void MapConditionalAccessPolicies(RouteGroupBuilder group)
    {
        group.MapGet("/conditional-access", async (IvfDbContext db) =>
        {
            var policies = await db.ConditionalAccessPolicies
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.Priority)
                .ToListAsync();
            return Results.Ok(policies);
        }).WithName("ListConditionalAccessPolicies");

        group.MapGet("/conditional-access/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var policy = await db.ConditionalAccessPolicies
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            return policy is null ? Results.NotFound() : Results.Ok(policy);
        }).WithName("GetConditionalAccessPolicy");

        group.MapPost("/conditional-access", async (CreateConditionalAccessRequest req, IvfDbContext db) =>
        {
            var policy = ConditionalAccessPolicy.Create(
                req.Name, req.Description, req.Priority, req.Action, null);

            policy.SetConditions(
                targetRoles: req.TargetRoles != null ? JsonSerializer.Serialize(req.TargetRoles) : null,
                allowedCountries: req.AllowedCountries != null ? JsonSerializer.Serialize(req.AllowedCountries) : null,
                blockedCountries: req.BlockedCountries != null ? JsonSerializer.Serialize(req.BlockedCountries) : null,
                allowedIpRanges: req.AllowedIpRanges != null ? JsonSerializer.Serialize(req.AllowedIpRanges) : null,
                allowedTimeWindows: req.AllowedTimeWindows != null ? JsonSerializer.Serialize(req.AllowedTimeWindows) : null,
                maxRiskLevel: req.MaxRiskLevel.ToString(),
                requireMfa: req.RequireMfa,
                requireCompliantDevice: req.RequireCompliantDevice,
                blockVpnTor: req.BlockVpnTor);

            db.ConditionalAccessPolicies.Add(policy);
            await db.SaveChangesAsync();
            return Results.Created($"/api/security/enterprise/conditional-access/{policy.Id}", policy);
        }).WithName("CreateConditionalAccessPolicy");

        group.MapPut("/conditional-access/{id:guid}", async (Guid id, UpdateConditionalAccessRequest req, IvfDbContext db) =>
        {
            var policy = await db.ConditionalAccessPolicies
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (policy is null) return Results.NotFound();

            policy.Update(req.Name, req.Description, req.Priority, req.Action, true, null);

            policy.SetConditions(
                targetRoles: req.TargetRoles != null ? JsonSerializer.Serialize(req.TargetRoles) : null,
                allowedCountries: req.AllowedCountries != null ? JsonSerializer.Serialize(req.AllowedCountries) : null,
                blockedCountries: req.BlockedCountries != null ? JsonSerializer.Serialize(req.BlockedCountries) : null,
                allowedIpRanges: req.AllowedIpRanges != null ? JsonSerializer.Serialize(req.AllowedIpRanges) : null,
                allowedTimeWindows: req.AllowedTimeWindows != null ? JsonSerializer.Serialize(req.AllowedTimeWindows) : null,
                maxRiskLevel: req.MaxRiskLevel.ToString(),
                requireMfa: req.RequireMfa,
                requireCompliantDevice: req.RequireCompliantDevice,
                blockVpnTor: req.BlockVpnTor);

            await db.SaveChangesAsync();
            return Results.Ok(policy);
        }).WithName("UpdateConditionalAccessPolicy");

        group.MapPost("/conditional-access/{id:guid}/enable", async (Guid id, IvfDbContext db) =>
        {
            var policy = await db.ConditionalAccessPolicies
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (policy is null) return Results.NotFound();
            policy.Enable();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Policy enabled" });
        }).WithName("EnableConditionalAccessPolicy");

        group.MapPost("/conditional-access/{id:guid}/disable", async (Guid id, IvfDbContext db) =>
        {
            var policy = await db.ConditionalAccessPolicies
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (policy is null) return Results.NotFound();
            policy.Disable();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Policy disabled" });
        }).WithName("DisableConditionalAccessPolicy");

        group.MapDelete("/conditional-access/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var policy = await db.ConditionalAccessPolicies
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (policy is null) return Results.NotFound();
            policy.MarkAsDeleted();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Policy deleted" });
        }).WithName("DeleteConditionalAccessPolicy");
    }

    // ─── Incident Response Rules ───

    private static void MapIncidentResponse(RouteGroupBuilder group)
    {
        group.MapGet("/incident-rules", async (IvfDbContext db) =>
        {
            var rules = await db.IncidentResponseRules
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.Priority)
                .ToListAsync();
            return Results.Ok(rules);
        }).WithName("ListIncidentResponseRules");

        group.MapPost("/incident-rules", async (CreateIncidentRuleRequest req, IvfDbContext db) =>
        {
            var rule = IncidentResponseRule.Create(
                req.Name, req.Description, req.Priority,
                JsonSerializer.Serialize(req.TriggerEventTypes),
                JsonSerializer.Serialize(req.TriggerSeverities),
                JsonSerializer.Serialize(req.Actions),
                req.IncidentSeverity, null);

            db.IncidentResponseRules.Add(rule);
            await db.SaveChangesAsync();
            return Results.Created($"/api/security/enterprise/incident-rules/{rule.Id}", rule);
        }).WithName("CreateIncidentResponseRule");

        group.MapPut("/incident-rules/{id:guid}", async (Guid id, UpdateIncidentRuleRequest req, IvfDbContext db) =>
        {
            var rule = await db.IncidentResponseRules
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (rule is null) return Results.NotFound();

            rule.Update(req.Name, req.Description, req.Priority,
                JsonSerializer.Serialize(req.TriggerEventTypes),
                JsonSerializer.Serialize(req.TriggerSeverities),
                req.TriggerThreshold, req.TriggerWindowMinutes,
                JsonSerializer.Serialize(req.Actions),
                req.IncidentSeverity, null, true);

            await db.SaveChangesAsync();
            return Results.Ok(rule);
        }).WithName("UpdateIncidentResponseRule");

        group.MapDelete("/incident-rules/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var rule = await db.IncidentResponseRules
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (rule is null) return Results.NotFound();
            rule.MarkAsDeleted();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Rule deleted" });
        }).WithName("DeleteIncidentResponseRule");
    }

    // ─── Security Incidents ───

    private static void MapSecurityIncidents(RouteGroupBuilder group)
    {
        group.MapGet("/incidents", async (
            int page, int pageSize, string? status, string? severity,
            IvfDbContext db) =>
        {
            var query = db.SecurityIncidents.Where(i => !i.IsDeleted);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(i => i.Status == status);
            if (!string.IsNullOrEmpty(severity))
                query = query.Where(i => i.Severity == severity);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Results.Ok(new { items, totalCount, page, pageSize });
        }).WithName("ListSecurityIncidents");

        group.MapGet("/incidents/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var incident = await db.SecurityIncidents
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            return incident is null ? Results.NotFound() : Results.Ok(incident);
        }).WithName("GetSecurityIncident");

        group.MapPost("/incidents/{id:guid}/investigate", async (
            Guid id, IvfDbContext db, HttpContext httpContext) =>
        {
            var assignedTo = GetUserId(httpContext);
            if (assignedTo is null) return Results.Unauthorized();
            var incident = await db.SecurityIncidents
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (incident is null) return Results.NotFound();
            incident.Investigate(assignedTo.Value);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Incident marked as investigating" });
        }).WithName("InvestigateIncident");

        group.MapPost("/incidents/{id:guid}/resolve", async (
            Guid id, ResolveIncidentRequest req, IvfDbContext db, HttpContext httpContext) =>
        {
            var resolvedBy = GetUserId(httpContext);
            if (resolvedBy is null) return Results.Unauthorized();
            var incident = await db.SecurityIncidents
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (incident is null) return Results.NotFound();
            incident.Resolve(req.Resolution, resolvedBy.Value);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Incident resolved" });
        }).WithName("ResolveIncident");

        group.MapPost("/incidents/{id:guid}/close", async (Guid id, IvfDbContext db) =>
        {
            var incident = await db.SecurityIncidents
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (incident is null) return Results.NotFound();
            incident.Close();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Incident closed" });
        }).WithName("CloseIncident");

        group.MapPost("/incidents/{id:guid}/false-positive", async (
            Guid id, ResolveIncidentRequest req,
            IvfDbContext db, HttpContext httpContext) =>
        {
            var markedBy = GetUserId(httpContext);
            if (markedBy is null) return Results.Unauthorized();
            var incident = await db.SecurityIncidents
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (incident is null) return Results.NotFound();
            incident.MarkFalsePositive(req.Resolution, markedBy.Value);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Incident marked as false positive" });
        }).WithName("MarkIncidentFalsePositive");
    }

    // ─── Data Retention Policies ───

    private static void MapDataRetention(RouteGroupBuilder group)
    {
        group.MapGet("/data-retention", async (IvfDbContext db) =>
        {
            var policies = await db.DataRetentionPolicies
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.EntityType)
                .ToListAsync();
            return Results.Ok(policies);
        }).WithName("ListDataRetentionPolicies");

        group.MapPost("/data-retention", async (CreateDataRetentionRequest req, IvfDbContext db) =>
        {
            // Check for duplicates
            var exists = await db.DataRetentionPolicies
                .AnyAsync(p => p.EntityType == req.EntityType && !p.IsDeleted);
            if (exists)
                return Results.Conflict(new { message = $"Policy for '{req.EntityType}' already exists" });

            var policy = DataRetentionPolicy.Create(
                req.EntityType, req.RetentionDays, req.Action, null);
            db.DataRetentionPolicies.Add(policy);
            await db.SaveChangesAsync();
            return Results.Created($"/api/security/enterprise/data-retention/{policy.Id}", policy);
        }).WithName("CreateDataRetentionPolicy");

        group.MapPut("/data-retention/{id:guid}", async (Guid id, UpdateDataRetentionRequest req, IvfDbContext db) =>
        {
            var policy = await db.DataRetentionPolicies
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (policy is null) return Results.NotFound();
            policy.Update(req.RetentionDays, req.Action, true);
            await db.SaveChangesAsync();
            return Results.Ok(policy);
        }).WithName("UpdateDataRetentionPolicy");

        group.MapDelete("/data-retention/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var policy = await db.DataRetentionPolicies
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (policy is null) return Results.NotFound();
            policy.MarkAsDeleted();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Policy deleted" });
        }).WithName("DeleteDataRetentionPolicy");
    }

    // ─── Impersonation ───

    private static void MapImpersonation(RouteGroupBuilder group)
    {
        group.MapGet("/impersonation", async (int page, int pageSize, string? status, IvfDbContext db) =>
        {
            var query = db.ImpersonationRequests.Where(r => !r.IsDeleted);
            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Results.Ok(new { items, totalCount, page, pageSize });
        }).WithName("ListImpersonationRequests");

        group.MapPost("/impersonation", async (
            CreateImpersonationRequest req,
            IvfDbContext db,
            HttpContext httpContext) =>
        {
            var requesterId = GetUserId(httpContext);
            if (requesterId is null)
                return Results.Unauthorized();

            // Verify target user exists
            var targetUser = await db.Users
                .FirstOrDefaultAsync(u => u.Id == req.TargetUserId && !u.IsDeleted);
            if (targetUser is null)
                return Results.NotFound(new { message = "Target user not found" });

            var request = ImpersonationRequest.Create(
                requestedBy: requesterId.Value,
                targetUserId: req.TargetUserId,
                reason: req.Reason);

            db.ImpersonationRequests.Add(request);
            await db.SaveChangesAsync();
            return Results.Created($"/api/security/enterprise/impersonation/{request.Id}", request);
        }).WithName("CreateImpersonationRequest");

        group.MapPost("/impersonation/{id:guid}/approve", async (
            Guid id, ApproveImpersonationRequest req,
            IvfDbContext db, HttpContext httpContext, IConfiguration config,
            ISecurityEventService securityEvents) =>
        {
            var approverId = GetUserId(httpContext);
            if (approverId is null) return Results.Unauthorized();

            var request = await db.ImpersonationRequests
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (request is null) return Results.NotFound();

            // Cannot self-approve
            if (request.RequestedBy == approverId.Value)
                return Results.BadRequest(new { message = "Cannot approve own request" });

            // Load the target user for JWT claims
            var targetUser = await db.Users
                .FirstOrDefaultAsync(u => u.Id == request.TargetUserId && !u.IsDeleted);
            if (targetUser is null) return Results.NotFound(new { message = "Target user not found" });

            request.Approve(approverId.Value, req.DurationMinutes);

            // Generate impersonation JWT
            var impersonationToken = GenerateImpersonationJwt(
                targetUser, request.RequestedBy, approverId.Value,
                req.DurationMinutes, config);

            request.Activate(impersonationToken);

            await db.SaveChangesAsync();

            // Log impersonation activation
            await securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.ImpersonationStarted,
                severity: "High",
                userId: request.RequestedBy,
                username: httpContext.User.FindFirst(ClaimTypes.Name)?.Value,
                ipAddress: httpContext.Connection.RemoteIpAddress?.ToString(),
                details: JsonSerializer.Serialize(new
                {
                    impersonatedUserId = request.TargetUserId,
                    impersonatedUsername = targetUser.Username,
                    approvedBy = approverId.Value,
                    durationMinutes = req.DurationMinutes
                })));

            return Results.Ok(new
            {
                message = "Impersonation approved and activated",
                token = impersonationToken,
                expiresAt = request.ExpiresAt
            });
        }).WithName("ApproveImpersonation");

        group.MapPost("/impersonation/{id:guid}/deny", async (
            Guid id, DenyImpersonationRequest req,
            IvfDbContext db, HttpContext httpContext) =>
        {
            var denierId = GetUserId(httpContext);
            if (denierId is null) return Results.Unauthorized();

            var request = await db.ImpersonationRequests
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (request is null) return Results.NotFound();
            request.Deny(denierId.Value, req.Reason);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Request denied" });
        }).WithName("DenyImpersonation");

        group.MapPost("/impersonation/{id:guid}/end", async (
            Guid id, IvfDbContext db, HttpContext httpContext, ISecurityEventService securityEvents) =>
        {
            var request = await db.ImpersonationRequests
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (request is null) return Results.NotFound();
            request.End("Admin ended session");
            await db.SaveChangesAsync();

            await securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.ImpersonationEnded,
                severity: "Medium",
                userId: request.RequestedBy,
                ipAddress: httpContext.Connection.RemoteIpAddress?.ToString(),
                details: JsonSerializer.Serialize(new
                {
                    impersonatedUserId = request.TargetUserId,
                    endedBy = GetUserId(httpContext)
                })));

            return Results.Ok(new { message = "Impersonation session ended" });
        }).WithName("EndImpersonation");
    }

    // ─── Permission Delegation ───

    private static void MapPermissionDelegation(RouteGroupBuilder group)
    {
        group.MapGet("/delegations", async (IvfDbContext db) =>
        {
            var delegations = await db.PermissionDelegations
                .Where(d => !d.IsDeleted && !d.IsRevoked && d.ValidUntil > DateTime.UtcNow)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
            return Results.Ok(delegations);
        }).WithName("ListActiveDelegations");

        group.MapPost("/delegations", async (
            CreateDelegationRequest req,
            IvfDbContext db,
            HttpContext httpContext) =>
        {
            var fromUserId = GetUserId(httpContext);
            if (fromUserId is null) return Results.Unauthorized();

            // Verify target user exists
            var toUser = await db.Users
                .FirstOrDefaultAsync(u => u.Id == req.ToUserId && !u.IsDeleted);
            if (toUser is null)
                return Results.NotFound(new { message = "Target user not found" });

            var delegation = PermissionDelegation.Create(
                fromUserId: fromUserId.Value,
                toUserId: req.ToUserId,
                permissionsJson: JsonSerializer.Serialize(req.Permissions),
                reason: req.Reason,
                validFrom: req.ValidFrom ?? DateTime.UtcNow,
                validUntil: req.ValidUntil);

            db.PermissionDelegations.Add(delegation);
            await db.SaveChangesAsync();
            return Results.Created($"/api/security/enterprise/delegations/{delegation.Id}", delegation);
        }).WithName("CreateDelegation");

        group.MapPost("/delegations/{id:guid}/revoke", async (Guid id, IvfDbContext db) =>
        {
            var delegation = await db.PermissionDelegations
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
            if (delegation is null) return Results.NotFound();
            delegation.Revoke(null);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Delegation revoked" });
        }).WithName("RevokeDelegation");
    }

    // ─── Behavioral Analytics ───

    private static void MapBehavioralAnalytics(RouteGroupBuilder group)
    {
        group.MapGet("/behavior-profiles", async (IvfDbContext db) =>
        {
            var profiles = await db.UserBehaviorProfiles
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.TotalLogins)
                .Take(100)
                .ToListAsync();
            return Results.Ok(profiles);
        }).WithName("ListBehaviorProfiles");

        group.MapGet("/behavior-profiles/{userId:guid}", async (Guid userId, IvfDbContext db) =>
        {
            var profile = await db.UserBehaviorProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        }).WithName("GetBehaviorProfile");

        group.MapPost("/behavior-profiles/{userId:guid}/refresh", async (
            Guid userId,
            IBehavioralAnalyticsService behavioralAnalytics) =>
        {
            await behavioralAnalytics.UpdateProfileAsync(userId);
            return Results.Ok(new { message = "Profile refreshed" });
        }).WithName("RefreshBehaviorProfile");
    }

    // ─── Notification Preferences ───

    private static void MapNotificationPreferences(RouteGroupBuilder group)
    {
        group.MapGet("/notification-preferences/{userId:guid}", async (Guid userId, IvfDbContext db) =>
        {
            var prefs = await db.NotificationPreferences
                .Where(p => p.UserId == userId && !p.IsDeleted)
                .ToListAsync();
            return Results.Ok(prefs);
        }).WithName("GetNotificationPreferences");

        group.MapPost("/notification-preferences", async (CreateNotificationPrefRequest req, IvfDbContext db) =>
        {
            // Check for duplicates
            var exists = await db.NotificationPreferences
                .AnyAsync(p => p.UserId == req.UserId && p.Channel == req.Channel && !p.IsDeleted);
            if (exists)
                return Results.Conflict(new { message = "Preference for this channel already exists" });

            var pref = NotificationPreference.Create(
                req.UserId, req.Channel,
                JsonSerializer.Serialize(req.EventTypes));
            db.NotificationPreferences.Add(pref);
            await db.SaveChangesAsync();
            return Results.Created($"/api/security/enterprise/notification-preferences/{pref.Id}", pref);
        }).WithName("CreateNotificationPreference");

        group.MapDelete("/notification-preferences/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var pref = await db.NotificationPreferences
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (pref is null) return Results.NotFound();
            pref.MarkAsDeleted();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Preference deleted" });
        }).WithName("DeleteNotificationPreference");
    }

    // ─── Helpers ───

    private static Guid? GetUserId(HttpContext context)
    {
        var claim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Generates a JWT for impersonation with special claims marking the acting admin 
    /// and the impersonated user. The token subject is the impersonated user,
    /// but "act.sub" identifies the admin performing impersonation.
    /// </summary>
    private static string GenerateImpersonationJwt(
        User targetUser, Guid requestedByAdminId, Guid approvedByAdminId,
        int durationMinutes, IConfiguration config)
    {
        var jwtSettings = config.GetSection("JwtSettings");
        var tokenHandler = new JwtSecurityTokenHandler();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, targetUser.Id.ToString()),
            new(ClaimTypes.Name, targetUser.Username),
            new(ClaimTypes.GivenName, targetUser.FullName),
            new(ClaimTypes.Role, targetUser.Role),
            new("department", targetUser.Department ?? ""),
            new("jti", Guid.NewGuid().ToString()),
            new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            // Impersonation-specific claims (RFC 8693 Actor Token pattern)
            new("act_sub", requestedByAdminId.ToString()),    // Admin performing impersonation
            new("act_approved_by", approvedByAdminId.ToString()),
            new("impersonation", "true"),
        };

        var keyService = JwtKeyService.Instance
            ?? throw new InvalidOperationException("JwtKeyService not initialized");

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(durationMinutes),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = keyService.SigningCredentials
        };

        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }
}

// ─── Request DTOs ───

public record CreateConditionalAccessRequest(
    string Name,
    string? Description,
    int Priority,
    int MaxRiskLevel,
    bool RequireMfa,
    bool RequireCompliantDevice,
    bool BlockVpnTor,
    string Action,
    List<string>? TargetRoles = null,
    List<string>? AllowedCountries = null,
    List<string>? BlockedCountries = null,
    List<string>? AllowedIpRanges = null,
    List<object>? AllowedTimeWindows = null);

public record UpdateConditionalAccessRequest(
    string Name,
    string? Description,
    int Priority,
    int MaxRiskLevel,
    bool RequireMfa,
    bool RequireCompliantDevice,
    bool BlockVpnTor,
    string Action,
    List<string>? TargetRoles = null,
    List<string>? AllowedCountries = null,
    List<string>? BlockedCountries = null,
    List<string>? AllowedIpRanges = null,
    List<object>? AllowedTimeWindows = null);

public record CreateIncidentRuleRequest(
    string Name,
    string? Description,
    int Priority,
    List<string> TriggerEventTypes,
    List<string> TriggerSeverities,
    int? TriggerThreshold,
    int? TriggerWindowMinutes,
    List<string> Actions,
    string IncidentSeverity);

public record UpdateIncidentRuleRequest(
    string Name,
    string? Description,
    int Priority,
    List<string> TriggerEventTypes,
    List<string> TriggerSeverities,
    int? TriggerThreshold,
    int? TriggerWindowMinutes,
    List<string> Actions,
    string IncidentSeverity);

public record ResolveIncidentRequest(string Resolution);

public record CreateDataRetentionRequest(
    string EntityType,
    int RetentionDays,
    string Action,
    string? Description);

public record UpdateDataRetentionRequest(
    int RetentionDays,
    string Action,
    string? Description);

public record CreateImpersonationRequest(
    Guid TargetUserId,
    string Reason,
    int DurationHours = 1);

public record DenyImpersonationRequest(string? Reason);

public record ApproveImpersonationRequest(int DurationMinutes = 30);

public record CreateDelegationRequest(
    Guid ToUserId,
    List<string> Permissions,
    DateTime? ValidFrom,
    DateTime ValidUntil,
    string? Reason);

public record CreateNotificationPrefRequest(
    Guid UserId,
    string Channel,
    List<string> EventTypes);
