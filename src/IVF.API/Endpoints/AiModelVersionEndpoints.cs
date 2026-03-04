using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class AiModelVersionEndpoints
{
    public static void MapAiModelVersionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai/model-versions")
            .WithTags("AI Model Versioning")
            .RequireAuthorization("AdminOnly");

        // List all versions (with optional filters)
        group.MapGet("/", async (IvfDbContext db, string? aiSystem, string? status, int page = 1, int pageSize = 20) =>
        {
            var query = db.AiModelVersions.Where(v => !v.IsDeleted).AsQueryable();

            if (!string.IsNullOrEmpty(aiSystem))
                query = query.Where(v => v.AiSystemName == aiSystem);
            if (!string.IsNullOrEmpty(status))
                query = query.Where(v => v.Status == status);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Results.Ok(new { items, totalCount, page, pageSize });
        });

        // Get specific version
        group.MapGet("/{id:guid}", async (IvfDbContext db, Guid id) =>
        {
            var version = await db.AiModelVersions.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            return version is null ? Results.NotFound() : Results.Ok(version);
        });

        // Create new version
        group.MapPost("/", async (IvfDbContext db, CreateModelVersionRequest request) =>
        {
            var version = AiModelVersion.Create(
                request.AiSystemName,
                request.ModelVersion,
                request.ChangeDescription,
                request.ConfigurationJson,
                request.ThresholdsJson,
                request.PreviousVersion);

            if (!string.IsNullOrEmpty(request.FeatureSetJson))
                version.SetFeatureSet(request.FeatureSetJson);

            if (!string.IsNullOrEmpty(request.GitCommitHash))
                version.SetGitReference(request.GitCommitHash, request.GitTag);

            db.AiModelVersions.Add(version);
            await db.SaveChangesAsync();
            return Results.Created($"/api/ai/model-versions/{version.Id}", version);
        });

        // Set performance metrics
        group.MapPut("/{id:guid}/metrics", async (IvfDbContext db, Guid id, SetMetricsRequest request) =>
        {
            var version = await db.AiModelVersions.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (version is null) return Results.NotFound();

            version.SetPerformanceMetrics(request.Accuracy, request.Precision, request.Recall, request.F1, request.Fpr, request.Fnr);
            await db.SaveChangesAsync();
            return Results.Ok(version);
        });

        // Link bias test
        group.MapPut("/{id:guid}/bias-test", async (IvfDbContext db, Guid id, LinkBiasTestRequest request) =>
        {
            var version = await db.AiModelVersions.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (version is null) return Results.NotFound();

            version.LinkBiasTest(request.BiasTestResultId, request.Passed);
            await db.SaveChangesAsync();
            return Results.Ok(version);
        });

        // Submit for review
        group.MapPost("/{id:guid}/submit", async (IvfDbContext db, Guid id) =>
        {
            var version = await db.AiModelVersions.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (version is null) return Results.NotFound();

            version.Submit();
            await db.SaveChangesAsync();
            return Results.Ok(version);
        });

        // Approve
        group.MapPost("/{id:guid}/approve", async (IvfDbContext db, HttpContext http, Guid id) =>
        {
            var version = await db.AiModelVersions.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (version is null) return Results.NotFound();

            var approver = http.User.Identity?.Name ?? "system";
            version.Approve(approver);
            await db.SaveChangesAsync();
            return Results.Ok(version);
        });

        // Reject
        group.MapPost("/{id:guid}/reject", async (IvfDbContext db, Guid id, RejectRequest request) =>
        {
            var version = await db.AiModelVersions.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (version is null) return Results.NotFound();

            version.Reject(request.Reason);
            await db.SaveChangesAsync();
            return Results.Ok(version);
        });

        // Deploy
        group.MapPost("/{id:guid}/deploy", async (IvfDbContext db, Guid id) =>
        {
            var version = await db.AiModelVersions.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (version is null) return Results.NotFound();

            // Retire currently deployed version of the same system
            var currentDeployed = await db.AiModelVersions
                .Where(v => v.AiSystemName == version.AiSystemName && v.Status == ModelVersionStatus.Deployed && !v.IsDeleted)
                .ToListAsync();
            foreach (var deployed in currentDeployed)
                deployed.Retire();

            version.Deploy();
            await db.SaveChangesAsync();
            return Results.Ok(version);
        });

        // Rollback
        group.MapPost("/{id:guid}/rollback", async (IvfDbContext db, Guid id, RollbackRequest request) =>
        {
            var version = await db.AiModelVersions.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (version is null) return Results.NotFound();

            version.Rollback(request.Reason);

            // Re-deploy previous version if specified
            if (!string.IsNullOrEmpty(version.PreviousVersion))
            {
                var previous = await db.AiModelVersions
                    .Where(v => v.AiSystemName == version.AiSystemName && v.ModelVersion == version.PreviousVersion && !v.IsDeleted)
                    .OrderByDescending(v => v.CreatedAt)
                    .FirstOrDefaultAsync();
                // Previous version can be redeployed via separate deploy call
            }

            await db.SaveChangesAsync();
            return Results.Ok(version);
        });

        // Changelog — version history for a system
        group.MapGet("/changelog/{aiSystem}", async (IvfDbContext db, string aiSystem) =>
        {
            var versions = await db.AiModelVersions
                .Where(v => v.AiSystemName == aiSystem && !v.IsDeleted)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => new
                {
                    v.Id,
                    v.ModelVersion,
                    v.PreviousVersion,
                    v.ChangeDescription,
                    v.Status,
                    v.Accuracy,
                    v.Fpr,
                    v.Fnr,
                    v.BiasTestPassed,
                    v.ApprovedBy,
                    v.ApprovedAt,
                    v.DeployedAt,
                    v.RetiredAt,
                    v.RollbackReason,
                    v.GitCommitHash,
                    v.GitTag,
                    v.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new
            {
                aiSystem,
                totalVersions = versions.Count,
                currentDeployed = versions.FirstOrDefault(v => v.Status == ModelVersionStatus.Deployed),
                versions
            });
        });

        // Dashboard — all AI systems version status
        group.MapGet("/dashboard", async (IvfDbContext db) =>
        {
            var allVersions = await db.AiModelVersions
                .Where(v => !v.IsDeleted)
                .ToListAsync();

            var systems = allVersions
                .GroupBy(v => v.AiSystemName)
                .Select(g => new
                {
                    aiSystem = g.Key,
                    totalVersions = g.Count(),
                    currentVersion = g.FirstOrDefault(v => v.Status == ModelVersionStatus.Deployed)?.ModelVersion ?? "none",
                    currentAccuracy = g.FirstOrDefault(v => v.Status == ModelVersionStatus.Deployed)?.Accuracy,
                    currentFpr = g.FirstOrDefault(v => v.Status == ModelVersionStatus.Deployed)?.Fpr,
                    currentFnr = g.FirstOrDefault(v => v.Status == ModelVersionStatus.Deployed)?.Fnr,
                    biasTestPassed = g.FirstOrDefault(v => v.Status == ModelVersionStatus.Deployed)?.BiasTestPassed ?? false,
                    pendingReview = g.Count(v => v.Status == ModelVersionStatus.PendingReview),
                    lastDeployedAt = g.Where(v => v.DeployedAt.HasValue).Max(v => v.DeployedAt),
                    rollbackCount = g.Count(v => v.Status == ModelVersionStatus.RolledBack)
                })
                .ToList();

            return Results.Ok(new
            {
                totalSystems = systems.Count,
                totalVersions = allVersions.Count,
                deployedCount = allVersions.Count(v => v.Status == ModelVersionStatus.Deployed),
                pendingReviewCount = allVersions.Count(v => v.Status == ModelVersionStatus.PendingReview),
                rollbackCount = allVersions.Count(v => v.Status == ModelVersionStatus.RolledBack),
                systems
            });
        });
    }

    public record CreateModelVersionRequest(
        string AiSystemName,
        string ModelVersion,
        string ChangeDescription,
        string ConfigurationJson,
        string ThresholdsJson,
        string? PreviousVersion = null,
        string? FeatureSetJson = null,
        string? GitCommitHash = null,
        string? GitTag = null);

    public record SetMetricsRequest(double Accuracy, double Precision, double Recall, double F1, double Fpr, double Fnr);
    public record LinkBiasTestRequest(Guid BiasTestResultId, bool Passed);
    public record RejectRequest(string Reason);
    public record RollbackRequest(string Reason);
}
