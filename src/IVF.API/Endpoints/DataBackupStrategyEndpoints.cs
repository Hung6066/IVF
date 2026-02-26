using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IVF.API.Endpoints;

public static class DataBackupStrategyEndpoints
{
    public static void MapDataBackupStrategyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/data-backup/strategies")
            .WithTags("Data Backup Strategies")
            .RequireAuthorization("AdminOnly");

        // ─── List all strategies ───────────────────────────────
        group.MapGet("/", async (IvfDbContext db, CancellationToken ct) =>
        {
            var strategies = await db.DataBackupStrategies
                .OrderBy(s => s.CreatedAt)
                .Select(s => new DataBackupStrategyDto(
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Enabled,
                    s.IncludeDatabase,
                    s.IncludeMinio,
                    s.CronExpression,
                    s.UploadToCloud,
                    s.RetentionDays,
                    s.MaxBackupCount,
                    s.LastRunAt,
                    s.LastRunOperationCode,
                    s.LastRunStatus,
                    s.CreatedAt,
                    s.UpdatedAt))
                .ToListAsync(ct);

            return Results.Ok(strategies);
        })
        .WithName("ListDataBackupStrategies");

        // ─── Get single strategy ───────────────────────────────
        group.MapGet("/{id:guid}", async (Guid id, IvfDbContext db, CancellationToken ct) =>
        {
            var s = await db.DataBackupStrategies.FindAsync([id], ct);
            if (s == null) return Results.NotFound(new { error = "Strategy not found" });

            return Results.Ok(new DataBackupStrategyDto(
                s.Id, s.Name, s.Description, s.Enabled,
                s.IncludeDatabase, s.IncludeMinio, s.CronExpression,
                s.UploadToCloud, s.RetentionDays, s.MaxBackupCount,
                s.LastRunAt, s.LastRunOperationCode, s.LastRunStatus,
                s.CreatedAt, s.UpdatedAt));
        })
        .WithName("GetDataBackupStrategy");

        // ─── Create strategy ───────────────────────────────────
        group.MapPost("/", async (CreateDataBackupStrategyRequest request, IvfDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Name is required" });

            if (string.IsNullOrWhiteSpace(request.CronExpression))
                return Results.BadRequest(new { error = "CronExpression is required" });

            if (!request.IncludeDatabase && !request.IncludeMinio)
                return Results.BadRequest(new { error = "At least one of includeDatabase or includeMinio must be true" });

            // Validate cron
            if (Services.BackupSchedulerService.GetNextCronTime(request.CronExpression, DateTime.UtcNow) == null)
                return Results.BadRequest(new { error = "Invalid cron expression" });

            var strategy = DataBackupStrategy.Create(
                request.Name,
                request.Description,
                request.IncludeDatabase,
                request.IncludeMinio,
                request.CronExpression,
                request.UploadToCloud,
                request.RetentionDays ?? 30,
                request.MaxBackupCount ?? 10);

            db.DataBackupStrategies.Add(strategy);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/admin/data-backup/strategies/{strategy.Id}",
                new { id = strategy.Id, message = "Strategy created" });
        })
        .WithName("CreateDataBackupStrategy");

        // ─── Update strategy ───────────────────────────────────
        group.MapPut("/{id:guid}", async (Guid id, UpdateDataBackupStrategyRequest request, IvfDbContext db, CancellationToken ct) =>
        {
            var strategy = await db.DataBackupStrategies.FindAsync([id], ct);
            if (strategy == null) return Results.NotFound(new { error = "Strategy not found" });

            // Validate cron if provided
            if (request.CronExpression != null &&
                Services.BackupSchedulerService.GetNextCronTime(request.CronExpression, DateTime.UtcNow) == null)
                return Results.BadRequest(new { error = "Invalid cron expression" });

            // Validate at least one target remains selected
            var willIncludeDb = request.IncludeDatabase ?? strategy.IncludeDatabase;
            var willIncludeMinio = request.IncludeMinio ?? strategy.IncludeMinio;
            if (!willIncludeDb && !willIncludeMinio)
                return Results.BadRequest(new { error = "At least one of includeDatabase or includeMinio must be true" });

            strategy.Update(
                name: request.Name,
                description: request.Description,
                enabled: request.Enabled,
                includeDatabase: request.IncludeDatabase,
                includeMinio: request.IncludeMinio,
                cronExpression: request.CronExpression,
                uploadToCloud: request.UploadToCloud,
                retentionDays: request.RetentionDays,
                maxBackupCount: request.MaxBackupCount);

            await db.SaveChangesAsync(ct);

            return Results.Ok(new { message = "Strategy updated" });
        })
        .WithName("UpdateDataBackupStrategy");

        // ─── Delete strategy ───────────────────────────────────
        group.MapDelete("/{id:guid}", async (Guid id, IvfDbContext db, CancellationToken ct) =>
        {
            var strategy = await db.DataBackupStrategies.FindAsync([id], ct);
            if (strategy == null) return Results.NotFound(new { error = "Strategy not found" });

            strategy.MarkAsDeleted();
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { message = "Strategy deleted" });
        })
        .WithName("DeleteDataBackupStrategy");

        // ─── Run strategy manually ─────────────────────────────
        group.MapPost("/{id:guid}/run", async (
            Guid id,
            IvfDbContext db,
            Services.DataBackupService dataBackupService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var strategy = await db.DataBackupStrategies.FindAsync([id], ct);
            if (strategy == null) return Results.NotFound(new { error = "Strategy not found" });

            var username = ctx.User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

            var operationCode = await dataBackupService.StartDataBackupAsync(
                strategy.IncludeDatabase,
                strategy.IncludeMinio,
                strategy.UploadToCloud,
                username,
                ct);

            // Record run immediately (status will be updated by scheduler/service)
            strategy.RecordRun(operationCode, "Running");
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { operationId = operationCode, message = $"Strategy '{strategy.Name}' started" });
        })
        .WithName("RunDataBackupStrategy");
    }
}

// ─── DTOs ────────────────────────────────────────────────

public record DataBackupStrategyDto(
    Guid Id,
    string Name,
    string? Description,
    bool Enabled,
    bool IncludeDatabase,
    bool IncludeMinio,
    string CronExpression,
    bool UploadToCloud,
    int RetentionDays,
    int MaxBackupCount,
    DateTime? LastRunAt,
    string? LastRunOperationCode,
    string? LastRunStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateDataBackupStrategyRequest(
    string Name,
    string? Description,
    bool IncludeDatabase = true,
    bool IncludeMinio = true,
    string CronExpression = "0 2 * * *",
    bool UploadToCloud = false,
    int? RetentionDays = 30,
    int? MaxBackupCount = 10);

public record UpdateDataBackupStrategyRequest(
    string? Name = null,
    string? Description = null,
    bool? Enabled = null,
    bool? IncludeDatabase = null,
    bool? IncludeMinio = null,
    string? CronExpression = null,
    bool? UploadToCloud = null,
    int? RetentionDays = null,
    int? MaxBackupCount = null);
