using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class DataSubjectRequestEndpoints
{
    public static void MapDataSubjectRequestEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/compliance/dsr")
            .WithTags("Data Subject Requests (GDPR)")
            .RequireAuthorization("AdminOnly");

        // List all DSRs with filters
        group.MapGet("/", async (
            [FromQuery] string? status,
            [FromQuery] string? requestType,
            [FromQuery] bool? overdue,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            IvfDbContext db) =>
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = db.DataSubjectRequests.Where(r => !r.IsDeleted);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);
            if (!string.IsNullOrEmpty(requestType))
                query = query.Where(r => r.RequestType == requestType);
            if (overdue == true)
                query = query.Where(r =>
                    r.Status != DsrStatus.Completed && r.Status != DsrStatus.Rejected
                    && DateTime.UtcNow > (r.ExtendedDeadline ?? r.Deadline));

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.ReceivedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.RequestReference,
                    r.PatientId,
                    r.DataSubjectName,
                    r.DataSubjectEmail,
                    r.RequestType,
                    r.Status,
                    r.IdentityVerified,
                    r.ReceivedAt,
                    r.Deadline,
                    r.ExtendedDeadline,
                    r.CompletedAt,
                    r.AssignedTo,
                    r.EscalatedToDpo,
                    IsOverdue = r.Status != DsrStatus.Completed && r.Status != DsrStatus.Rejected
                                && DateTime.UtcNow > (r.ExtendedDeadline ?? r.Deadline),
                    DaysRemaining = Math.Max(0, (int)((r.ExtendedDeadline ?? r.Deadline) - DateTime.UtcNow).TotalDays)
                })
                .ToListAsync();

            return Results.Ok(new { items, totalCount, page, pageSize });
        });

        // Get single DSR
        group.MapGet("/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var dsr = await db.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            return dsr is null ? Results.NotFound() : Results.Ok(dsr);
        });

        // Create new DSR
        group.MapPost("/", async ([FromBody] CreateDsrRequest req, IvfDbContext db) =>
        {
            var reference = $"DSR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
            var dsr = DataSubjectRequest.Create(
                reference,
                req.DataSubjectName,
                req.DataSubjectEmail,
                req.RequestType,
                req.Description,
                req.PatientId);

            db.DataSubjectRequests.Add(dsr);
            await db.SaveChangesAsync();
            return Results.Created($"/api/compliance/dsr/{dsr.Id}", new { dsr.Id, dsr.RequestReference });
        });

        // Verify identity
        group.MapPost("/{id:guid}/verify-identity", async (
            Guid id,
            [FromBody] VerifyIdentityRequest req,
            IvfDbContext db) =>
        {
            var dsr = await db.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (dsr is null) return Results.NotFound();

            dsr.VerifyIdentity(req.Method, req.VerifiedBy);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Identity verified", dsr.Status });
        });

        // Assign handler
        group.MapPost("/{id:guid}/assign", async (
            Guid id,
            [FromBody] AssignDsrRequest req,
            IvfDbContext db) =>
        {
            var dsr = await db.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (dsr is null) return Results.NotFound();

            dsr.AssignHandler(req.UserId);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Handler assigned", dsr.Status });
        });

        // Extend deadline
        group.MapPost("/{id:guid}/extend", async (
            Guid id,
            [FromBody] ExtendDeadlineRequest req,
            IvfDbContext db) =>
        {
            var dsr = await db.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (dsr is null) return Results.NotFound();

            dsr.ExtendDeadline(req.Reason);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Deadline extended", dsr.ExtendedDeadline });
        });

        // Complete DSR
        group.MapPost("/{id:guid}/complete", async (
            Guid id,
            [FromBody] CompleteDsrRequest req,
            IvfDbContext db) =>
        {
            var dsr = await db.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (dsr is null) return Results.NotFound();

            dsr.Complete(req.ResponseSummary);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "DSR completed", dsr.CompletedAt });
        });

        // Reject DSR (manifestly unfounded/excessive)
        group.MapPost("/{id:guid}/reject", async (
            Guid id,
            [FromBody] RejectDsrRequest req,
            IvfDbContext db) =>
        {
            var dsr = await db.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (dsr is null) return Results.NotFound();

            dsr.Reject(req.Reason, req.LegalBasis);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "DSR rejected", dsr.Status });
        });

        // Escalate to DPO
        group.MapPost("/{id:guid}/escalate", async (Guid id, IvfDbContext db) =>
        {
            var dsr = await db.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (dsr is null) return Results.NotFound();

            dsr.EscalateToDpo();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Escalated to DPO", dsr.Status });
        });

        // Mark data subject notified
        group.MapPost("/{id:guid}/notify", async (Guid id, IvfDbContext db) =>
        {
            var dsr = await db.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (dsr is null) return Results.NotFound();

            dsr.NotifyDataSubject();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Data subject notified", dsr.NotifiedAt });
        });

        // Add internal note
        group.MapPost("/{id:guid}/notes", async (
            Guid id,
            [FromBody] AddNoteRequest req,
            IvfDbContext db) =>
        {
            var dsr = await db.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (dsr is null) return Results.NotFound();

            dsr.AddNote(req.Note);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Note added" });
        });

        // DSR Dashboard & metrics
        group.MapGet("/dashboard", async (IvfDbContext db) =>
        {
            var all = await db.DataSubjectRequests.Where(r => !r.IsDeleted).ToListAsync();

            return Results.Ok(new
            {
                total = all.Count,
                byStatus = all.GroupBy(r => r.Status)
                    .Select(g => new { status = g.Key, count = g.Count() }),
                byType = all.GroupBy(r => r.RequestType)
                    .Select(g => new { type = g.Key, count = g.Count() }),
                overdue = all.Count(r => r.IsOverdue),
                escalated = all.Count(r => r.EscalatedToDpo),
                avgCompletionDays = all
                    .Where(r => r.CompletedAt.HasValue)
                    .Select(r => (r.CompletedAt!.Value - r.ReceivedAt).TotalDays)
                    .DefaultIfEmpty(0)
                    .Average(),
                complianceRate = all.Count == 0 ? 100.0 :
                    Math.Round((double)all.Count(r => !r.IsOverdue) / all.Count * 100, 1),
                pendingCount = all.Count(r => r.Status != DsrStatus.Completed && r.Status != DsrStatus.Rejected),
                last30Days = all.Count(r => r.ReceivedAt >= DateTime.UtcNow.AddDays(-30))
            });
        });
    }

    // ── Request DTOs ──
    public record CreateDsrRequest(
        string DataSubjectName,
        string DataSubjectEmail,
        string RequestType,
        string Description,
        Guid? PatientId);

    public record VerifyIdentityRequest(string Method, Guid VerifiedBy);
    public record AssignDsrRequest(Guid UserId);
    public record ExtendDeadlineRequest(string Reason);
    public record CompleteDsrRequest(string ResponseSummary);
    public record RejectDsrRequest(string Reason, string? LegalBasis);
    public record AddNoteRequest(string Note);
}
