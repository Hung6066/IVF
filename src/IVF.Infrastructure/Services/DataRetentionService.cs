using System.Text;
using System.Text.Json;
using IVF.Application.Common;
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
/// Supports Archive action: exports records to S3 (MinIO) before hard-deleting.
/// </summary>
public sealed class DataRetentionService(
    IServiceScopeFactory scopeFactory,
    ILogger<DataRetentionService> logger) : IDataRetentionService, IHostedService, IDisposable
{
    private Timer? _timer;

    public Task StartAsync(CancellationToken ct)
    {
        logger.LogInformation("Data retention service starting — will run daily at 2 AM UTC");
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
            var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

            await using var lockHandle = await lockService.TryAcquireAsync(
                "data-retention-daily", TimeSpan.FromMinutes(30));

            if (lockHandle is null)
            {
                logger.LogInformation("Data retention skipped — another instance holds the lock");
                return;
            }

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
        var storage = scope.ServiceProvider.GetService<IObjectStorageService>();

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
                    "SecurityEvent" => await PurgeSecurityEventsAsync(context, storage, cutoff, policy.Action, ct),
                    "UserLoginHistory" => await PurgeLoginHistoryAsync(context, storage, cutoff, policy.Action, ct),
                    "UserSession" => await PurgeSessionsAsync(context, cutoff, ct),
                    "AuditLog" => await PurgeAuditLogsAsync(context, storage, cutoff, policy.Action, ct),
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

    private static async Task ArchiveToS3Async<T>(
        IObjectStorageService? storage,
        string entityType,
        IReadOnlyList<T> records,
        CancellationToken ct) where T : class
    {
        if (storage is null || records.Count == 0) return;

        await storage.EnsureBucketExistsAsync(StorageBuckets.AuditArchive, ct);

        var date = DateTime.UtcNow;
        var objectKey = $"retention/{entityType}/{date:yyyy/MM/dd}/{entityType}_{date:yyyyMMdd_HHmmss}.jsonl";
        var jsonLines = new StringBuilder();
        foreach (var record in records)
        {
            jsonLines.AppendLine(JsonSerializer.Serialize(record));
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonLines.ToString()));
        await storage.UploadAsync(
            StorageBuckets.AuditArchive,
            objectKey,
            stream,
            "application/x-ndjson",
            stream.Length,
            new Dictionary<string, string>
            {
                ["x-amz-meta-entity-type"] = entityType,
                ["x-amz-meta-record-count"] = records.Count.ToString(),
                ["x-amz-meta-archived-at"] = date.ToString("o")
            },
            ct);
    }

    private static async Task<int> PurgeSecurityEventsAsync(
        IvfDbContext db, IObjectStorageService? storage, DateTime cutoff, string action, CancellationToken ct)
    {
        var events = await db.SecurityEvents
            .Where(e => e.CreatedAt < cutoff && !e.IsDeleted)
            .ToListAsync(ct);

        if (events.Count == 0) return 0;

        if (action == "Anonymize")
        {
            foreach (var e in events) e.MarkAsDeleted();
            return events.Count;
        }

        if (action == "Archive")
        {
            await ArchiveToS3Async(storage, "SecurityEvent", events, ct);
        }

        db.SecurityEvents.RemoveRange(events);
        return events.Count;
    }

    private static async Task<int> PurgeLoginHistoryAsync(
        IvfDbContext db, IObjectStorageService? storage, DateTime cutoff, string action, CancellationToken ct)
    {
        var records = await db.UserLoginHistories
            .Where(h => h.LoginAt < cutoff && !h.IsDeleted)
            .ToListAsync(ct);

        if (records.Count == 0) return 0;

        if (action == "Anonymize")
        {
            foreach (var r in records) r.MarkAsDeleted();
            return records.Count;
        }

        if (action == "Archive")
        {
            await ArchiveToS3Async(storage, "UserLoginHistory", records, ct);
        }

        db.UserLoginHistories.RemoveRange(records);
        return records.Count;
    }

    private static async Task<int> PurgeSessionsAsync(IvfDbContext db, DateTime cutoff, CancellationToken ct)
    {
        var sessions = await db.UserSessions
            .Where(s => s.ExpiresAt < cutoff && !s.IsDeleted)
            .ToListAsync(ct);

        if (sessions.Count == 0) return 0;

        db.UserSessions.RemoveRange(sessions);
        return sessions.Count;
    }

    private static async Task<int> PurgeAuditLogsAsync(
        IvfDbContext db, IObjectStorageService? storage, DateTime cutoff, string action, CancellationToken ct)
    {
        var logs = await db.AuditLogs
            .Where(l => l.CreatedAt < cutoff && !l.IsDeleted)
            .ToListAsync(ct);

        if (logs.Count == 0) return 0;

        if (action == "Anonymize")
        {
            foreach (var l in logs) l.MarkAsDeleted();
            return logs.Count;
        }

        if (action == "Archive")
        {
            await ArchiveToS3Async(storage, "AuditLog", logs, ct);
        }

        db.AuditLogs.RemoveRange(logs);
        return logs.Count;
    }

    public void Dispose() => _timer?.Dispose();
}
