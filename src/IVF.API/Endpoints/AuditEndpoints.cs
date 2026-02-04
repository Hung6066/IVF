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
            string? from = null,
            string? to = null,
            int page = 1,
            int pageSize = 50) =>
        {
            DateTime? fromDate = null;
            DateTime? toDate = null;

            if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var f))
                fromDate = DateTime.SpecifyKind(f, DateTimeKind.Utc);
            
            if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var t))
                toDate = DateTime.SpecifyKind(t, DateTimeKind.Utc);

            var logs = await repo.SearchAsync(entityType, action, userId, fromDate, toDate, page, pageSize);
            return Results.Ok(logs);
        });
    }
}
