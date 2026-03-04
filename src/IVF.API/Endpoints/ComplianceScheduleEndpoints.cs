using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class ComplianceScheduleEndpoints
{
    public static void MapComplianceScheduleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/compliance/schedule")
            .WithTags("Compliance Schedule (Phase 4)")
            .RequireAuthorization("AdminOnly");

        // List all tasks with filters
        group.MapGet("/", async (
            [FromQuery] string? framework,
            [FromQuery] string? frequency,
            [FromQuery] string? category,
            [FromQuery] string? status,
            [FromQuery] bool? overdue,
            [FromQuery] bool? upcoming,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            IvfDbContext db) =>
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = db.ComplianceSchedules.Where(s => !s.IsDeleted);

            if (!string.IsNullOrEmpty(framework))
                query = query.Where(s => s.Framework == framework);
            if (!string.IsNullOrEmpty(frequency))
                query = query.Where(s => s.Frequency == frequency);
            if (!string.IsNullOrEmpty(category))
                query = query.Where(s => s.Category == category);
            if (!string.IsNullOrEmpty(status))
                query = query.Where(s => s.Status == status);
            if (overdue == true)
                query = query.Where(s => s.NextDueDate != null
                    && DateTime.UtcNow > s.NextDueDate
                    && s.Status == ScheduleStatus.Active);
            if (upcoming == true)
                query = query.Where(s => s.NextDueDate != null
                    && s.NextDueDate <= DateTime.UtcNow.AddDays(14)
                    && DateTime.UtcNow <= s.NextDueDate
                    && s.Status == ScheduleStatus.Active);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(s => s.NextDueDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.Id,
                    s.TaskName,
                    s.Description,
                    s.Framework,
                    s.Frequency,
                    s.Category,
                    s.Owner,
                    s.AssignedUserId,
                    s.Status,
                    s.LastCompletedAt,
                    s.NextDueDate,
                    s.CompletionCount,
                    s.Priority,
                    IsOverdue = s.NextDueDate != null && DateTime.UtcNow > s.NextDueDate && s.Status == ScheduleStatus.Active,
                    IsUpcoming = s.NextDueDate != null && DateTime.UtcNow <= s.NextDueDate
                        && s.NextDueDate <= DateTime.UtcNow.AddDays(s.ReminderDaysBefore)
                        && s.Status == ScheduleStatus.Active
                })
                .ToListAsync();

            return Results.Ok(new { items, totalCount, page, pageSize });
        });

        // Get single task
        group.MapGet("/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var task = await db.ComplianceSchedules
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            return task is null ? Results.NotFound() : Results.Ok(task);
        });

        // Create new scheduled task
        group.MapPost("/", async ([FromBody] CreateScheduleRequest req, IvfDbContext db) =>
        {
            var schedule = ComplianceSchedule.Create(
                req.TaskName,
                req.Description,
                req.Framework,
                req.Frequency,
                req.Category,
                req.Owner,
                req.NextDueDate,
                req.EvidenceRequired,
                req.Priority ?? "Medium");

            db.ComplianceSchedules.Add(schedule);
            await db.SaveChangesAsync();
            return Results.Created($"/api/compliance/schedule/{schedule.Id}", new { schedule.Id });
        });

        // Mark task completed
        group.MapPost("/{id:guid}/complete", async (
            Guid id,
            [FromBody] CompleteScheduleRequest req,
            IvfDbContext db) =>
        {
            var task = await db.ComplianceSchedules
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (task is null) return Results.NotFound();

            task.MarkCompleted(req.CompletedBy, req.Notes);
            await db.SaveChangesAsync();
            return Results.Ok(new
            {
                message = "Task completed",
                task.CompletionCount,
                task.NextDueDate
            });
        });

        // Assign task
        group.MapPost("/{id:guid}/assign", async (
            Guid id,
            [FromBody] AssignScheduleRequest req,
            IvfDbContext db) =>
        {
            var task = await db.ComplianceSchedules
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (task is null) return Results.NotFound();

            task.Assign(req.UserId);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Task assigned" });
        });

        // Pause schedule
        group.MapPost("/{id:guid}/pause", async (Guid id, IvfDbContext db) =>
        {
            var task = await db.ComplianceSchedules
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (task is null) return Results.NotFound();

            task.Pause();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Schedule paused" });
        });

        // Resume schedule
        group.MapPost("/{id:guid}/resume", async (Guid id, IvfDbContext db) =>
        {
            var task = await db.ComplianceSchedules
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (task is null) return Results.NotFound();

            task.Resume();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Schedule resumed" });
        });

        // Update schedule
        group.MapPut("/{id:guid}/schedule", async (
            Guid id,
            [FromBody] UpdateScheduleRequest req,
            IvfDbContext db) =>
        {
            var task = await db.ComplianceSchedules
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (task is null) return Results.NotFound();

            task.UpdateSchedule(req.Frequency, req.NextDueDate, req.ReminderDaysBefore);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Schedule updated" });
        });

        // Seed default Phase 4 compliance schedule
        group.MapPost("/seed-defaults", async (IvfDbContext db) =>
        {
            if (await db.ComplianceSchedules.AnyAsync(s => !s.IsDeleted))
                return Results.Ok(new { message = "Schedule already seeded" });

            var baseDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            var defaults = new List<ComplianceSchedule>
            {
                // Monthly
                ComplianceSchedule.Create("Security Metrics Review", "Review security KPIs, vulnerability counts, incident trends", "ALL", ComplianceFrequency.Monthly, ComplianceCategory.Review, "CISO", baseDate, "Security dashboard export, KPI report", "High"),
                ComplianceSchedule.Create("Vulnerability Scan Review", "Review SAST/DAST/SCA scan results and remediation", "SOC2", ComplianceFrequency.Monthly, ComplianceCategory.Monitoring, "DevSecOps", baseDate, "Scan reports from GitHub Actions", "High"),
                ComplianceSchedule.Create("AI Model Performance Monitor", "Review AI system metrics, FPR/FNR, drift detection", "NIST_AI_RMF", ComplianceFrequency.Monthly, ComplianceCategory.Monitoring, "AI Committee", baseDate, "AiModelVersion dashboard export", "Medium"),
                ComplianceSchedule.Create("Compliance Dashboard Review", "Review compliance scoring engine output", "ALL", ComplianceFrequency.Monthly, ComplianceCategory.Review, "Compliance", baseDate, "ComplianceDashboard API response", "Medium"),
                ComplianceSchedule.Create("DSR Status Review", "Review pending data subject requests, escalate overdue", "GDPR", ComplianceFrequency.Monthly, ComplianceCategory.Review, "DPO", baseDate, "DSR dashboard export", "High"),

                // Quarterly
                ComplianceSchedule.Create("Management Review (ISO 27001 9.3)", "ISMS management review meeting", "ISO27001", ComplianceFrequency.Quarterly, ComplianceCategory.Review, "CISO", baseDate.AddMonths(3), "Meeting minutes, action items", "Critical"),
                ComplianceSchedule.Create("Internal Audit Cycle", "Plan and execute internal audit per ISO 27001", "ISO27001", ComplianceFrequency.Quarterly, ComplianceCategory.Audit, "Internal Audit", baseDate.AddMonths(3), "Audit report, NCR log", "Critical"),
                ComplianceSchedule.Create("Access Review", "Quarterly RBAC access certification", "SOC2", ComplianceFrequency.Quarterly, ComplianceCategory.Review, "IT Admin", baseDate.AddMonths(3), "User access matrix, changes log", "High"),
                ComplianceSchedule.Create("AI Bias Re-Testing", "Re-run bias tests on all AI systems", "NIST_AI_RMF", ComplianceFrequency.Quarterly, ComplianceCategory.Testing, "AI Committee", baseDate.AddMonths(3), "AiBiasTestResult records", "High"),
                ComplianceSchedule.Create("Risk Re-Assessment", "Update risk register and threat assessment", "ALL", ComplianceFrequency.Quarterly, ComplianceCategory.Assessment, "CISO", baseDate.AddMonths(3), "Updated risk assessment document", "High"),
                ComplianceSchedule.Create("Security Training Update", "Update training content, track completion", "HIPAA", ComplianceFrequency.Quarterly, ComplianceCategory.Training, "HR/Compliance", baseDate.AddMonths(3), "Training completion records", "Medium"),

                // Semi-Annual
                ComplianceSchedule.Create("Penetration Testing", "External penetration test execution", "SOC2", ComplianceFrequency.SemiAnnual, ComplianceCategory.Testing, "External", baseDate.AddMonths(6), "Pentest report (template IVF-PT-001)", "Critical"),

                // Annual
                ComplianceSchedule.Create("SOC 2 Type II Renewal", "Annual SOC 2 audit engagement", "SOC2", ComplianceFrequency.Annual, ComplianceCategory.Audit, "External Auditor", baseDate.AddYears(1), "SOC 2 Type II report", "Critical"),
                ComplianceSchedule.Create("ISO 27001 Surveillance Audit", "Annual surveillance audit", "ISO27001", ComplianceFrequency.Annual, ComplianceCategory.Audit, "Certification Body", baseDate.AddYears(1), "Surveillance audit report", "Critical"),
                ComplianceSchedule.Create("HIPAA Risk Assessment", "Annual HIPAA Security Rule risk assessment", "HIPAA", ComplianceFrequency.Annual, ComplianceCategory.Assessment, "Compliance", baseDate.AddYears(1), "Updated HIPAA self-assessment", "Critical"),
                ComplianceSchedule.Create("GDPR DPIA Review", "Annual review of Data Protection Impact Assessment", "GDPR", ComplianceFrequency.Annual, ComplianceCategory.Assessment, "DPO", baseDate.AddYears(1), "Updated DPIA document", "High"),
                ComplianceSchedule.Create("HITRUST Reassessment", "Annual HITRUST CSF reassessment", "HITRUST", ComplianceFrequency.Annual, ComplianceCategory.Assessment, "Compliance", baseDate.AddYears(1), "Updated HITRUST self-assessment", "High"),
                ComplianceSchedule.Create("AI Governance Review", "Annual AI governance charter and policy review", "ISO42001", ComplianceFrequency.Annual, ComplianceCategory.Review, "AI Committee", baseDate.AddYears(1), "Updated AI governance charter", "High"),
                ComplianceSchedule.Create("BCP/DRP Testing Exercise", "Annual business continuity drill", "ISO27001", ComplianceFrequency.Annual, ComplianceCategory.Testing, "Operations", baseDate.AddYears(1), "Drill report, lessons learned", "Critical"),
                ComplianceSchedule.Create("Training Renewal", "Annual compliance training for all staff", "ALL", ComplianceFrequency.Annual, ComplianceCategory.Training, "HR/Compliance", baseDate.AddYears(1), "Training completion certificates", "High"),
            };

            db.ComplianceSchedules.AddRange(defaults);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Seeded default schedule", count = defaults.Count });
        });

        // Dashboard — compliance schedule overview
        group.MapGet("/dashboard", async (IvfDbContext db) =>
        {
            var all = await db.ComplianceSchedules
                .Where(s => !s.IsDeleted && s.Status == ScheduleStatus.Active)
                .ToListAsync();

            return Results.Ok(new
            {
                totalActive = all.Count,
                overdue = all.Count(s => s.IsOverdue),
                upcoming = all.Count(s => s.IsUpcoming),
                byFramework = all.GroupBy(s => s.Framework)
                    .Select(g => new { framework = g.Key, total = g.Count(), overdue = g.Count(s => s.IsOverdue) }),
                byFrequency = all.GroupBy(s => s.Frequency)
                    .Select(g => new { frequency = g.Key, total = g.Count(), overdue = g.Count(s => s.IsOverdue) }),
                byCategory = all.GroupBy(s => s.Category)
                    .Select(g => new { category = g.Key, total = g.Count() }),
                nextDueTasks = all.Where(s => s.NextDueDate.HasValue)
                    .OrderBy(s => s.NextDueDate)
                    .Take(10)
                    .Select(s => new { s.Id, s.TaskName, s.Framework, s.NextDueDate, s.Priority }),
                completionRate = all.Count == 0 ? 100.0 :
                    Math.Round((double)all.Count(s => !s.IsOverdue) / all.Count * 100, 1)
            });
        });
    }

    // Request DTOs
    public record CreateScheduleRequest(
        string TaskName,
        string Description,
        string Framework,
        string Frequency,
        string Category,
        string Owner,
        DateTime NextDueDate,
        string? EvidenceRequired,
        string? Priority);

    public record CompleteScheduleRequest(Guid CompletedBy, string? Notes);
    public record AssignScheduleRequest(Guid UserId);
    public record UpdateScheduleRequest(string Frequency, DateTime NextDueDate, int ReminderDaysBefore);
}
