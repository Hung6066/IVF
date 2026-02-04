using System.Security.Claims;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;

namespace IVF.API.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        // Get notifications for current user
        group.MapGet("/", async (ClaimsPrincipal principal, INotificationRepository repo, bool? unreadOnly) =>
        {
            var userId = GetUserId(principal);
            if (userId == null) return Results.Unauthorized();
            return Results.Ok(await repo.GetByUserAsync(userId.Value, unreadOnly ?? false));
        });

        // Get unread count
        group.MapGet("/unread-count", async (ClaimsPrincipal principal, INotificationRepository repo) =>
        {
            var userId = GetUserId(principal);
            if (userId == null) return Results.Unauthorized();
            var count = await repo.GetUnreadCountAsync(userId.Value);
            return Results.Ok(new { count });
        });

        // Get notification by ID
        group.MapGet("/{id:guid}", async (Guid id, INotificationRepository repo) =>
        {
            var notification = await repo.GetByIdAsync(id);
            return notification is null ? Results.NotFound() : Results.Ok(notification);
        });

        // Mark single notification as read
        group.MapPost("/{id:guid}/read", async (Guid id, ClaimsPrincipal principal, INotificationRepository repo, IUnitOfWork uow) =>
        {
            var userId = GetUserId(principal);
            if (userId == null) return Results.Unauthorized();
            
            var notification = await repo.GetByIdAsync(id);
            if (notification is null) return Results.NotFound();
            if (notification.UserId != userId) return Results.Forbid();
            
            notification.MarkAsRead();
            await uow.SaveChangesAsync();
            return Results.Ok(notification);
        });

        // Mark all notifications as read
        group.MapPost("/read-all", async (ClaimsPrincipal principal, INotificationRepository repo, IUnitOfWork uow) =>
        {
            var userId = GetUserId(principal);
            if (userId == null) return Results.Unauthorized();
            
            await repo.MarkAsReadAsync(userId.Value);
            await uow.SaveChangesAsync();
            return Results.NoContent();
        });

        // Create notification (admin only)
        group.MapPost("/", async (CreateNotificationRequest req, INotificationRepository repo, IUnitOfWork uow) =>
        {
            var notification = Notification.Create(
                req.UserId,
                req.Title,
                req.Message,
                req.Type,
                req.EntityType,
                req.EntityId);
            
            await repo.AddAsync(notification);
            await uow.SaveChangesAsync();
            return Results.Created($"/api/notifications/{notification.Id}", notification);
        }).RequireAuthorization("AdminOnly");

        // Broadcast notification to multiple users (admin only)
        group.MapPost("/broadcast", async (BroadcastRequest req, INotificationRepository repo, IUnitOfWork uow) =>
        {
            var notifications = req.UserIds.Select(userId => Notification.Create(
                userId,
                req.Title,
                req.Message,
                req.Type));
            
            await repo.AddManyAsync(notifications);
            await uow.SaveChangesAsync();
            return Results.Ok(new { sent = req.UserIds.Count });
        }).RequireAuthorization("AdminOnly");
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    public record CreateNotificationRequest(
        Guid UserId,
        string Title,
        string Message,
        NotificationType Type,
        string? EntityType = null,
        Guid? EntityId = null);

    public record BroadcastRequest(
        List<Guid> UserIds,
        string Title,
        string Message,
        NotificationType Type);
}
