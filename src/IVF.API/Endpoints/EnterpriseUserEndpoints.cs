using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Users.Commands;
using IVF.Application.Features.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

/// <summary>
/// Enterprise User Management API — Google/Amazon/Facebook-grade user management.
/// Covers: Sessions, Groups, Login History, Analytics, Consent, User Detail.
/// </summary>
public static class EnterpriseUserEndpoints
{
    public static void MapEnterpriseUserEndpoints(this IEndpointRouteBuilder app)
    {
        // ═══════════════════════════════════════════════════
        // USER ANALYTICS & DETAIL
        // ═══════════════════════════════════════════════════
        var analytics = app.MapGroup("/api/user-analytics").WithTags("UserAnalytics").RequireAuthorization("AdminOnly");

        analytics.MapGet("/", async (IMediator m) =>
        {
            var result = await m.Send(new GetUserAnalyticsQuery());
            return Results.Ok(result);
        });

        analytics.MapGet("/users/{userId}", async (IMediator m, Guid userId) =>
        {
            var result = await m.Send(new GetUserDetailQuery(userId));
            return Results.Ok(result);
        });

        // ═══════════════════════════════════════════════════
        // SESSION MANAGEMENT
        // ═══════════════════════════════════════════════════
        var sessions = app.MapGroup("/api/user-sessions").WithTags("UserSessions").RequireAuthorization("AdminOnly");

        sessions.MapGet("/{userId}", async (IMediator m, Guid userId, bool? activeOnly) =>
        {
            var result = await m.Send(new GetUserSessionsQuery(userId, activeOnly ?? true));
            return Results.Ok(result);
        });

        sessions.MapDelete("/{sessionId}", async (IMediator m, Guid sessionId, [FromQuery] string? reason) =>
        {
            await m.Send(new RevokeUserSessionCommand(sessionId, reason ?? "Admin revoked", "admin"));
            return Results.NoContent();
        });

        sessions.MapDelete("/user/{userId}/all", async (IMediator m, Guid userId, [FromQuery] string? reason) =>
        {
            var count = await m.Send(new RevokeAllUserSessionsCommand(userId, reason ?? "Admin revoked all", "admin"));
            return Results.Ok(new { revokedCount = count });
        });

        // ═══════════════════════════════════════════════════
        // GROUP MANAGEMENT
        // ═══════════════════════════════════════════════════
        var groups = app.MapGroup("/api/user-groups").WithTags("UserGroups").RequireAuthorization("AdminOnly");

        groups.MapGet("/", async (IMediator m, string? search, string? groupType, int page = 1, int pageSize = 20) =>
        {
            var result = await m.Send(new GetUserGroupsQuery(search, groupType, page, pageSize));
            return Results.Ok(result);
        });

        groups.MapGet("/{groupId}", async (IMediator m, Guid groupId) =>
        {
            var result = await m.Send(new GetUserGroupDetailQuery(groupId));
            return Results.Ok(result);
        });

        groups.MapPost("/", async (IMediator m, [FromBody] CreateUserGroupCommand command) =>
        {
            var id = await m.Send(command);
            return Results.Created($"/api/user-groups/{id}", new { id });
        });

        groups.MapPut("/{groupId}", async (IMediator m, Guid groupId, [FromBody] UpdateUserGroupCommand command) =>
        {
            if (groupId != command.Id) return Results.BadRequest();
            await m.Send(command);
            return Results.NoContent();
        });

        groups.MapDelete("/{groupId}", async (IMediator m, Guid groupId) =>
        {
            await m.Send(new DeleteUserGroupCommand(groupId));
            return Results.NoContent();
        });

        // Group Members
        groups.MapPost("/{groupId}/members", async (IMediator m, Guid groupId, [FromBody] AddGroupMemberRequest request) =>
        {
            var id = await m.Send(new AddGroupMemberCommand(groupId, request.UserId, request.MemberRole ?? "member", request.AddedBy));
            return Results.Created($"/api/user-groups/{groupId}/members/{id}", new { id });
        });

        groups.MapDelete("/{groupId}/members/{userId}", async (IMediator m, Guid groupId, Guid userId) =>
        {
            await m.Send(new RemoveGroupMemberCommand(groupId, userId));
            return Results.NoContent();
        });

        groups.MapPut("/{groupId}/members/{userId}/role", async (IMediator m, Guid groupId, Guid userId, [FromBody] UpdateMemberRoleRequest request) =>
        {
            await m.Send(new UpdateGroupMemberRoleCommand(groupId, userId, request.MemberRole));
            return Results.NoContent();
        });

        // Group Permissions
        groups.MapPost("/{groupId}/permissions", async (IMediator m, Guid groupId, [FromBody] AssignGroupPermissionsRequest request) =>
        {
            await m.Send(new AssignGroupPermissionsCommand(groupId, request.Permissions, request.GrantedBy));
            return Results.Ok(new { message = "Group permissions updated", count = request.Permissions.Count });
        });

        // Group Consent (bulk consent for all members)
        groups.MapGet("/{groupId}/consents", async (IMediator m, Guid groupId) =>
        {
            var result = await m.Send(new GetGroupConsentStatusQuery(groupId));
            return Results.Ok(result);
        });

        groups.MapPost("/{groupId}/consents", async (IMediator m, Guid groupId, [FromBody] GrantGroupConsentRequest request) =>
        {
            var count = await m.Send(new GrantGroupConsentCommand(
                groupId, request.ConsentType, request.ConsentVersion,
                request.IpAddress, request.UserAgent, request.ExpiresAt));
            return Results.Ok(new { message = $"Consent granted to {count} members", count });
        });

        groups.MapDelete("/{groupId}/consents/{consentType}", async (IMediator m, Guid groupId, string consentType, [FromQuery] string? reason) =>
        {
            var count = await m.Send(new RevokeGroupConsentCommand(groupId, consentType, reason));
            return Results.Ok(new { message = $"Consent revoked from {count} members", count });
        });

        // ═══════════════════════════════════════════════════
        // LOGIN HISTORY
        // ═══════════════════════════════════════════════════
        var loginHistory = app.MapGroup("/api/login-history").WithTags("LoginHistory").RequireAuthorization("AdminOnly");

        loginHistory.MapGet("/", async (IMediator m, Guid? userId, int page = 1, int pageSize = 50, bool? isSuccess = null, bool? isSuspicious = null) =>
        {
            var result = await m.Send(new GetUserLoginHistoryQuery(userId, page, pageSize, isSuccess, isSuspicious));
            return Results.Ok(result);
        });

        // ═══════════════════════════════════════════════════
        // CONSENT MANAGEMENT (GDPR/HIPAA)
        // ═══════════════════════════════════════════════════
        var consent = app.MapGroup("/api/user-consents").WithTags("UserConsents").RequireAuthorization();

        // Current user's valid consent types (for menu/UI consent checking)
        consent.MapGet("/my-status", async (HttpContext ctx, IConsentValidationService consentService) =>
        {
            var userIdClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var allTypes = new[] { "data_processing", "medical_records", "marketing", "analytics",
                                   "research", "third_party", "biometric_data", "cookies" };
            var missing = await consentService.GetMissingConsentsAsync(userId, allTypes);
            var valid = allTypes.Except(missing).ToList();
            return Results.Ok(new { validConsents = valid, missingConsents = missing });
        });

        consent.MapGet("/{userId}", async (IMediator m, Guid userId) =>
        {
            var result = await m.Send(new GetUserConsentsQuery(userId));
            return Results.Ok(result);
        });

        consent.MapPost("/", async (IMediator m, [FromBody] GrantConsentCommand command) =>
        {
            var id = await m.Send(command);
            return Results.Created($"/api/user-consents/{id}", new { id });
        });

        consent.MapDelete("/{consentId}", async (IMediator m, Guid consentId, [FromQuery] string? reason) =>
        {
            await m.Send(new RevokeConsentCommand(consentId, reason));
            return Results.NoContent();
        });
    }
}

// Request DTOs
public record AddGroupMemberRequest(Guid UserId, string? MemberRole, Guid? AddedBy);
public record UpdateMemberRoleRequest(string MemberRole);
public record AssignGroupPermissionsRequest(List<string> Permissions, Guid? GrantedBy);
public record GrantGroupConsentRequest(string ConsentType, string? ConsentVersion, string? IpAddress, string? UserAgent, DateTime? ExpiresAt);
