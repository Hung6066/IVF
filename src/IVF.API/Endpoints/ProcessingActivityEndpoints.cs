using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class ProcessingActivityEndpoints
{
    public static void MapProcessingActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/compliance/ropa")
            .WithTags("ROPA")
            .RequireAuthorization("AdminOnly");

        // List processing activities
        group.MapGet("/", async (IvfDbContext db, string? status, string? legalBasis) =>
        {
            var query = db.ProcessingActivities.Where(p => !p.IsDeleted).AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.Status == status);
            if (!string.IsNullOrEmpty(legalBasis))
                query = query.Where(p => p.LegalBasis == legalBasis);

            var activities = await query.OrderBy(p => p.ActivityName).ToListAsync();
            return Results.Ok(activities);
        }).WithName("ListProcessingActivities");

        // Get single activity
        group.MapGet("/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var activity = await db.ProcessingActivities.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            return activity is null ? Results.NotFound() : Results.Ok(activity);
        }).WithName("GetProcessingActivity");

        // Create processing activity
        group.MapPost("/", async (CreateProcessingActivityRequest req, IvfDbContext db) =>
        {
            var activity = ProcessingActivity.Create(
                req.ActivityName, req.Purpose, req.LegalBasis,
                req.DataCategories, req.DataSubjectCategories,
                req.RetentionPeriod, req.DataControllerName,
                req.RequiresDpia, req.IsAutomatedDecisionMaking);

            if (!string.IsNullOrEmpty(req.SecurityMeasures))
                activity.SetSecurityMeasures(req.SecurityMeasures);
            if (!string.IsNullOrEmpty(req.Recipients))
                activity.SetRecipients(req.Recipients);
            if (!string.IsNullOrEmpty(req.ThirdCountryTransfers))
                activity.SetTransferDetails(req.ThirdCountryTransfers);

            db.ProcessingActivities.Add(activity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/compliance/ropa/{activity.Id}", activity);
        }).WithName("CreateProcessingActivity");

        // Activate
        group.MapPost("/{id:guid}/activate", async (Guid id, IvfDbContext db) =>
        {
            var activity = await db.ProcessingActivities.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (activity is null) return Results.NotFound();
            activity.Activate();
            await db.SaveChangesAsync();
            return Results.Ok(activity);
        }).WithName("ActivateProcessingActivity");

        // Submit for review
        group.MapPost("/{id:guid}/review", async (Guid id, IvfDbContext db) =>
        {
            var activity = await db.ProcessingActivities.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (activity is null) return Results.NotFound();
            activity.SubmitForReview();
            await db.SaveChangesAsync();
            return Results.Ok(activity);
        }).WithName("SubmitForReviewProcessingActivity");

        // Mark reviewed
        group.MapPost("/{id:guid}/mark-reviewed", async (Guid id, IvfDbContext db, DateTime? nextReviewDue) =>
        {
            var activity = await db.ProcessingActivities.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (activity is null) return Results.NotFound();
            activity.MarkReviewed(nextReviewDue);
            await db.SaveChangesAsync();
            return Results.Ok(activity);
        }).WithName("MarkReviewedProcessingActivity");

        // Complete DPIA
        group.MapPost("/{id:guid}/complete-dpia", async (Guid id, CompleteDpiaRequest req, IvfDbContext db) =>
        {
            var activity = await db.ProcessingActivities.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (activity is null) return Results.NotFound();
            activity.CompleteDpia(req.Reference);
            await db.SaveChangesAsync();
            return Results.Ok(activity);
        }).WithName("CompleteDpiaForActivity");

        // Archive
        group.MapPost("/{id:guid}/archive", async (Guid id, IvfDbContext db) =>
        {
            var activity = await db.ProcessingActivities.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (activity is null) return Results.NotFound();
            activity.Archive();
            await db.SaveChangesAsync();
            return Results.Ok(activity);
        }).WithName("ArchiveProcessingActivity");

        // Dashboard / summary
        group.MapGet("/dashboard", async (IvfDbContext db) =>
        {
            var activities = await db.ProcessingActivities
                .Where(p => !p.IsDeleted)
                .ToListAsync();

            var active = activities.Where(p => p.Status == ProcessingActivityStatus.Active).ToList();

            var dashboard = new
            {
                total = activities.Count,
                byStatus = activities.GroupBy(p => p.Status)
                    .Select(g => new { status = g.Key, count = g.Count() }),
                byLegalBasis = activities.GroupBy(p => p.LegalBasis)
                    .Select(g => new { legalBasis = g.Key, count = g.Count() }),
                requiresDpia = activities.Count(p => p.RequiresDpia),
                dpiaCompleted = activities.Count(p => p.DpiaCompleted),
                dpiaPending = activities.Count(p => p.RequiresDpia && !p.DpiaCompleted),
                automatedDecisionMaking = activities.Count(p => p.IsAutomatedDecisionMaking),
                overdueForReview = active.Count(p => p.IsOverdueForReview()),
                lastReviewDate = active
                    .Where(p => p.LastReviewedAt.HasValue)
                    .OrderByDescending(p => p.LastReviewedAt)
                    .Select(p => p.LastReviewedAt)
                    .FirstOrDefault()
            };

            return Results.Ok(dashboard);
        }).WithName("RopaDashboard");

        // Overdue for review
        group.MapGet("/overdue", async (IvfDbContext db) =>
        {
            var activities = await db.ProcessingActivities
                .Where(p => !p.IsDeleted && p.Status == ProcessingActivityStatus.Active)
                .ToListAsync();

            var overdue = activities.Where(p => p.IsOverdueForReview())
                .OrderBy(p => p.NextReviewDueAt)
                .ToList();

            return Results.Ok(overdue);
        }).WithName("OverdueProcessingActivities");
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────

public record CreateProcessingActivityRequest(
    string ActivityName,
    string Purpose,
    string LegalBasis,
    string DataCategories,
    string DataSubjectCategories,
    string RetentionPeriod,
    string DataControllerName,
    bool RequiresDpia = false,
    bool IsAutomatedDecisionMaking = false,
    string? SecurityMeasures = null,
    string? Recipients = null,
    string? ThirdCountryTransfers = null);

public record CompleteDpiaRequest(string Reference);
