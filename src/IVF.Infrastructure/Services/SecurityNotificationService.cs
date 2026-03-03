using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Security notification service — sends security alerts through configured channels.
/// Dispatches via in-app notifications (DB + SignalR) based on user preferences.
/// </summary>
public sealed class SecurityNotificationService(
    IvfDbContext context,
    INotificationService notificationService,
    ILogger<SecurityNotificationService> logger) : ISecurityNotificationService
{
    public async Task SendSecurityAlertAsync(Guid userId, string eventType, string message, CancellationToken ct = default)
    {
        // Load user notification preferences
        var preferences = await context.Set<NotificationPreference>()
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .ToListAsync(ct);

        if (preferences.Count == 0)
        {
            // Default: send in-app notification
            await SendInAppNotificationAsync(userId, eventType, message, ct);
            return;
        }

        foreach (var pref in preferences)
        {
            var eventTypes = System.Text.Json.JsonSerializer
                .Deserialize<List<string>>(pref.EventTypes ?? "[]");

            if (eventTypes is null || eventTypes.Count == 0 ||
                eventTypes.Contains(eventType) || eventTypes.Contains("*"))
            {
                switch (pref.Channel.ToLowerInvariant())
                {
                    case "in_app":
                    case "inapp":
                    case "app":
                        await SendInAppNotificationAsync(userId, eventType, message, ct);
                        break;

                    case "email":
                        logger.LogInformation("[Email] Security alert queued for user {UserId}: [{EventType}] {Message}",
                            userId, eventType, message);
                        // Email dispatch would integrate with SendGrid/SMTP here
                        break;

                    case "sms":
                        logger.LogInformation("[SMS] Security alert queued for user {UserId}: [{EventType}]",
                            userId, eventType);
                        // SMS dispatch would integrate with Twilio here
                        break;

                    default:
                        await SendInAppNotificationAsync(userId, eventType, message, ct);
                        break;
                }
            }
        }
    }

    public async Task SendAdminAlertAsync(string eventType, string message, CancellationToken ct = default)
    {
        var adminUserIds = await context.Users
            .Where(u => u.Role == "Admin" && !u.IsDeleted)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var adminId in adminUserIds)
        {
            await SendSecurityAlertAsync(adminId, eventType, $"[ADMIN] {message}", ct);
        }
    }

    private async Task SendInAppNotificationAsync(Guid userId, string eventType, string message, CancellationToken ct)
    {
        try
        {
            await notificationService.SendNotificationAsync(
                userId,
                $"🛡️ Cảnh báo bảo mật: {eventType}",
                message,
                NotificationType.SecurityAlert,
                entityType: "SecurityEvent",
                ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send in-app security notification for user {UserId}", userId);
        }
    }
}
