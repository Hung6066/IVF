using IVF.Application.Common.Interfaces;

namespace IVF.API.Endpoints;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit").WithTags("Audit").RequireAuthorization("AdminOnly");

        // Get recent audit logs
        group.MapGet("/recent", async (IAuditLogRepository repo, int take = 100) =>
            Results.Ok(await repo.GetRecentAsync(take)));

        // Get audit logs for specific entity
        group.MapGet("/entity/{entityType}/{entityId:guid}", async (string entityType, Guid entityId, IAuditLogRepository repo) =>
            Results.Ok(await repo.GetByEntityAsync(entityType, entityId)));

        // Get audit logs by user
        group.MapGet("/user/{userId:guid}", async (Guid userId, IAuditLogRepository repo, int take = 100) =>
            Results.Ok(await repo.GetByUserAsync(userId, take)));

        // Search audit logs
        group.MapGet("/search", async (
            IAuditLogRepository repo,
            string? entityType,
            string? action,
            Guid? userId,
            DateTime? from,
            DateTime? to,
            int page = 1,
            int pageSize = 50) =>
        {
            var logs = await repo.SearchAsync(entityType, action, userId, from, to, page, pageSize);
            return Results.Ok(logs);
        });
    }
}
