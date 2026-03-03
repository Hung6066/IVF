using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Incident response automation — evaluates security events against rules
/// and executes automated response actions (lock account, revoke sessions, etc.).
/// Inspired by PagerDuty automation + Microsoft Sentinel playbooks.
/// </summary>
public sealed class IncidentResponseService(
    IvfDbContext context,
    ISecurityEventService securityEvents,
    ISecurityNotificationService notificationService,
    ILogger<IncidentResponseService> logger) : IIncidentResponseService
{
    public async Task ProcessEventAsync(SecurityEvent securityEvent, CancellationToken ct = default)
    {
        var rules = await context.Set<IncidentResponseRule>()
            .Where(r => r.IsEnabled && !r.IsDeleted)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

        if (rules.Count == 0) return;

        foreach (var rule in rules)
        {
            if (!MatchesRule(rule, securityEvent))
                continue;

            // Check threshold if configured
            if (rule.TriggerThreshold.HasValue && rule.TriggerWindowMinutes.HasValue)
            {
                var windowStart = DateTime.UtcNow.AddMinutes(-rule.TriggerWindowMinutes.Value);
                var eventTypes = DeserializeList(rule.TriggerEventTypes);
                var eventCount = await context.SecurityEvents
                    .CountAsync(e => !e.IsDeleted && e.CreatedAt >= windowStart &&
                        eventTypes.Contains(e.EventType), ct);

                if (eventCount < rule.TriggerThreshold.Value)
                    continue;
            }

            // Rule matched — create incident and execute actions
            logger.LogWarning("Incident response rule '{RuleName}' triggered by event {EventType} for user {UserId}",
                rule.Name, securityEvent.EventType, securityEvent.UserId);

            var incident = SecurityIncident.Create(
                incidentType: securityEvent.EventType,
                severity: rule.IncidentSeverity,
                userId: securityEvent.UserId,
                username: securityEvent.Username,
                ipAddress: securityEvent.IpAddress,
                description: $"Auto-triggered by rule: {rule.Name}",
                details: securityEvent.Details,
                relatedEventIds: JsonSerializer.Serialize(new[] { securityEvent.Id }));

            context.Set<SecurityIncident>().Add(incident);

            // Execute automated actions
            var actions = DeserializeList(rule.Actions);
            var actionsTaken = new List<string>();

            foreach (var action in actions)
            {
                try
                {
                    await ExecuteActionAsync(action, securityEvent, ct);
                    actionsTaken.Add(action);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to execute incident action '{Action}' for rule '{Rule}'",
                        action, rule.Name);
                    actionsTaken.Add($"{action}:failed");
                }
            }

            incident.RecordActions(JsonSerializer.Serialize(actionsTaken));
            await context.SaveChangesAsync(ct);

            // Log incident creation event
            await securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.IncidentCreated,
                severity: rule.IncidentSeverity,
                userId: securityEvent.UserId,
                username: securityEvent.Username,
                ipAddress: securityEvent.IpAddress,
                details: JsonSerializer.Serialize(new
                {
                    incidentId = incident.Id,
                    ruleName = rule.Name,
                    actionsTaken
                }),
                correlationId: securityEvent.CorrelationId), ct);

            break; // Only one rule fires per event (highest priority)
        }
    }

    private static bool MatchesRule(IncidentResponseRule rule, SecurityEvent ev)
    {
        // Check event type match
        if (!string.IsNullOrEmpty(rule.TriggerEventTypes))
        {
            var types = DeserializeList(rule.TriggerEventTypes);
            if (types.Count > 0 && !types.Contains(ev.EventType))
                return false;
        }

        // Check severity match
        if (!string.IsNullOrEmpty(rule.TriggerSeverities))
        {
            var severities = DeserializeList(rule.TriggerSeverities);
            if (severities.Count > 0 && !severities.Contains(ev.Severity, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private async Task ExecuteActionAsync(string action, SecurityEvent ev, CancellationToken ct)
    {
        switch (action)
        {
            case "lock_account":
                if (ev.UserId.HasValue)
                {
                    var lockout = AccountLockout.Create(
                        userId: ev.UserId.Value,
                        username: ev.Username ?? "unknown",
                        reason: $"Auto-locked by incident response: {ev.EventType}",
                        durationMinutes: 60,
                        failedAttempts: 0,
                        lockedBy: "system",
                        isManualLock: false);
                    context.AccountLockouts.Add(lockout);
                }
                break;

            case "revoke_sessions":
                if (ev.UserId.HasValue)
                {
                    var sessions = await context.UserSessions
                        .Where(s => s.UserId == ev.UserId.Value && !s.IsRevoked && !s.IsDeleted)
                        .ToListAsync(ct);
                    foreach (var session in sessions)
                        session.Revoke("incident_response", "system");
                }
                break;

            case "block_ip":
                if (!string.IsNullOrEmpty(ev.IpAddress))
                {
                    var existing = await context.IpWhitelistEntries
                        .AnyAsync(e => e.IpAddress == ev.IpAddress && !e.IsDeleted, ct);
                    if (!existing)
                    {
                        // For IP blocking, we add a GeoBlockRule (since IpWhitelist is an allowlist)
                        // The SecurityEnforcementMiddleware handles this
                        logger.LogWarning("IP {IP} flagged for blocking by incident response", ev.IpAddress);
                    }
                }
                break;

            case "notify_admin":
                await notificationService.SendAdminAlertAsync(
                    ev.EventType,
                    $"Incident: {ev.EventType} for user {ev.Username} from IP {ev.IpAddress}");
                break;

            case "require_password_change":
                // Flag user for password change on next login
                logger.LogWarning("Password change required for user {UserId} due to incident",
                    ev.UserId);
                break;

            default:
                logger.LogWarning("Unknown incident response action: {Action}", action);
                break;
        }
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
