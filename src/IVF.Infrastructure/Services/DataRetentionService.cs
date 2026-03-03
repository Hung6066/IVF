using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Data retention service — executes automated purging based on retention policies.
/// Runs daily as a hosted background service.
/// HIPAA requires 7-year retention for medical audit data; GDPR requires data minimization.
/// </summary>
public sealed class DataRetentionService(
    IServiceScopeFactory scopeFactory,
    ILogger<DataRetentionService> logger) : IDataRetentionService, IHostedService, IDisposable
{
    private Timer? _timer;

    public Task StartAsync(CancellationToken ct)
    {
        logger.LogInformation("Data retention service starting — will run daily at 2 AM UTC");
        // Run daily at 2 AM UTC
        var now = DateTime.UtcNow;
        var next2Am = now.Date.AddHours(2);
        if (next2Am <= now) next2Am = next2Am.AddDays(1);
        var delay = next2Am - now;

        _timer = new Timer(async _ => await RunAsync(), null, delay, TimeSpan.FromDays(1));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();
            var result = await service.ExecutePoliciesAsync();
            logger.LogInformation("Data retention completed: {Policies} policies, {Records} records purged",
                result.PoliciesExecuted, result.TotalRecordsPurged);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Data retention service failed");
        }
    }

    public async Task<DataRetentionResult> ExecutePoliciesAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var policies = await context.Set<DataRetentionPolicy>()
            .Where(p => p.IsEnabled && !p.IsDeleted)
            .ToListAsync(ct);

        var totalPurged = 0;
        var errors = new List<string>();

        foreach (var policy in policies)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-policy.RetentionDays);
                var purged = policy.EntityType switch
                {
                    "SecurityEvent" => await PurgeSecurityEventsAsync(context, cutoff, policy.Action, ct),
                    "UserLoginHistory" => await PurgeLoginHistoryAsync(context, cutoff, policy.Action, ct),
                    "UserSession" => await PurgeSessionsAsync(context, cutoff, ct),
                    "AuditLog" => await PurgeAuditLogsAsync(context, cutoff, policy.Action, ct),
                    _ => 0
                };

                policy.RecordExecution(purged);
                totalPurged += purged;

                if (purged > 0)
                {
                    logger.LogInformation("Purged {Count} {EntityType} records older than {Days} days (action: {Action})",
                        purged, policy.EntityType, policy.RetentionDays, policy.Action);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute retention policy for {EntityType}", policy.EntityType);
                errors.Add($"{policy.EntityType}: {ex.Message}");
            }
        }

        await context.SaveChangesAsync(ct);
        return new DataRetentionResult(policies.Count, totalPurged, errors);
    }

    private static async Task<int> PurgeSecurityEventsAsync(IvfDbContext db, DateTime cutoff, string action, CancellationToken ct)
    {
        var events = await db.SecurityEvents
            .Where(e => e.CreatedAt < cutoff && !e.IsDeleted)
            .ToListAsync(ct);

        if (action == "Anonymize")
        {
            foreach (var e in events) e.MarkAsDeleted(); // Soft delete = anonymize
            return events.Count;
        }

        // Hard delete
        db.SecurityEvents.RemoveRange(events);
        return events.Count;
    }

    private static async Task<int> PurgeLoginHistoryAsync(IvfDbContext db, DateTime cutoff, string action, CancellationToken ct)
    {
        var records = await db.UserLoginHistories
            .Where(h => h.LoginAt < cutoff && !h.IsDeleted)
            .ToListAsync(ct);

        if (action == "Anonymize")
        {
            foreach (var r in records) r.MarkAsDeleted();
            return records.Count;
        }

        db.UserLoginHistories.RemoveRange(records);
        return records.Count;
    }

    private static async Task<int> PurgeSessionsAsync(IvfDbContext db, DateTime cutoff, CancellationToken ct)
    {
        var sessions = await db.UserSessions
            .Where(s => s.ExpiresAt < cutoff && !s.IsDeleted)
            .ToListAsync(ct);

        db.UserSessions.RemoveRange(sessions);
        return sessions.Count;
    }

    private static async Task<int> PurgeAuditLogsAsync(IvfDbContext db, DateTime cutoff, string action, CancellationToken ct)
    {
        var logs = await db.AuditLogs
            .Where(l => l.CreatedAt < cutoff && !l.IsDeleted)
            .ToListAsync(ct);

        if (action == "Anonymize")
        {
            foreach (var l in logs) l.MarkAsDeleted();
            return logs.Count;
        }

        db.AuditLogs.RemoveRange(logs);
        return logs.Count;
    }

    public void Dispose() => _timer?.Dispose();
}
