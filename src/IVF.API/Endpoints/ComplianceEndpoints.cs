using System.Text.Json;
using IVF.API.Services;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class ComplianceEndpoints
{
    public static void MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/compliance")
            .WithTags("Compliance")
            .RequireAuthorization("AdminOnly");

        MapBreachNotifications(group);
        MapComplianceTraining(group);
        MapPasswordPolicy(group);
        MapComplianceDashboard(group);
    }

    // ─── Breach Notification Management ─────────────────────────────────

    private static void MapBreachNotifications(RouteGroupBuilder group)
    {
        group.MapGet("/breaches", async (IvfDbContext db) =>
        {
            var breaches = await db.BreachNotifications
                .Where(b => !b.IsDeleted)
                .OrderByDescending(b => b.DetectedAt)
                .ToListAsync();
            return Results.Ok(breaches);
        }).WithName("ListBreaches");

        group.MapGet("/breaches/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var breach = await db.BreachNotifications
                .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);
            return breach is null ? Results.NotFound() : Results.Ok(breach);
        }).WithName("GetBreach");

        group.MapPost("/breaches", async (CreateBreachRequest req, IvfDbContext db) =>
        {
            var breach = BreachNotification.Create(
                req.IncidentId,
                req.BreachType,
                req.Severity,
                req.AffectedRecordCount,
                req.AffectedDataTypes != null ? JsonSerializer.Serialize(req.AffectedDataTypes) : null,
                req.AffectedSystems != null ? JsonSerializer.Serialize(req.AffectedSystems) : null,
                req.RootCause,
                req.AttackVector);

            db.BreachNotifications.Add(breach);
            await db.SaveChangesAsync();
            return Results.Created($"/api/compliance/breaches/{breach.Id}", breach);
        }).WithName("CreateBreach");

        group.MapPost("/breaches/{id:guid}/assess", async (Guid id, AssessBreachRequest req, IvfDbContext db) =>
        {
            var breach = await db.BreachNotifications.FindAsync(id);
            if (breach is null) return Results.NotFound();

            breach.Assess(
                req.AffectedRecordCount,
                req.AffectedDataTypes != null ? JsonSerializer.Serialize(req.AffectedDataTypes) : null,
                req.AffectedUserIds != null ? JsonSerializer.Serialize(req.AffectedUserIds) : null);

            await db.SaveChangesAsync();
            return Results.Ok(breach);
        }).WithName("AssessBreach");

        group.MapPost("/breaches/{id:guid}/contain", async (Guid id, ContainBreachRequest req, IvfDbContext db) =>
        {
            var breach = await db.BreachNotifications.FindAsync(id);
            if (breach is null) return Results.NotFound();

            breach.Contain(req.RemediationSteps != null ? JsonSerializer.Serialize(req.RemediationSteps) : null);
            await db.SaveChangesAsync();
            return Results.Ok(breach);
        }).WithName("ContainBreach");

        group.MapPost("/breaches/{id:guid}/notify-dpa", async (Guid id, NotifyDpaRequest req, IvfDbContext db) =>
        {
            var breach = await db.BreachNotifications.FindAsync(id);
            if (breach is null) return Results.NotFound();

            breach.NotifyDpa(req.Reference);
            await db.SaveChangesAsync();
            return Results.Ok(breach);
        }).WithName("NotifyDpa");

        group.MapPost("/breaches/{id:guid}/notify-subjects", async (Guid id, NotifySubjectsRequest req, IvfDbContext db) =>
        {
            var breach = await db.BreachNotifications.FindAsync(id);
            if (breach is null) return Results.NotFound();

            breach.NotifySubjects(req.Count);
            await db.SaveChangesAsync();
            return Results.Ok(breach);
        }).WithName("NotifySubjects");

        group.MapPost("/breaches/{id:guid}/notify-hhs", async (Guid id, IvfDbContext db) =>
        {
            var breach = await db.BreachNotifications.FindAsync(id);
            if (breach is null) return Results.NotFound();

            breach.NotifyHhs();
            await db.SaveChangesAsync();
            return Results.Ok(breach);
        }).WithName("NotifyHhs");

        group.MapPost("/breaches/{id:guid}/resolve", async (Guid id, ResolveBreachRequest req, IvfDbContext db, HttpContext ctx) =>
        {
            var breach = await db.BreachNotifications.FindAsync(id);
            if (breach is null) return Results.NotFound();

            var userId = Guid.Parse(ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst("userId")?.Value ?? Guid.Empty.ToString());
            breach.Resolve(
                req.LessonsLearned,
                req.PreventionMeasures != null ? JsonSerializer.Serialize(req.PreventionMeasures) : null,
                userId);

            await db.SaveChangesAsync();
            return Results.Ok(breach);
        }).WithName("ResolveBreach");

        group.MapPost("/breaches/{id:guid}/close", async (Guid id, IvfDbContext db) =>
        {
            var breach = await db.BreachNotifications.FindAsync(id);
            if (breach is null) return Results.NotFound();

            breach.Close();
            await db.SaveChangesAsync();
            return Results.Ok(breach);
        }).WithName("CloseBreach");

        group.MapGet("/breaches/overdue", async (IvfDbContext db) =>
        {
            var overdue = await db.BreachNotifications
                .Where(b => !b.IsDeleted
                    && b.Status != "Resolved" && b.Status != "Closed"
                    && b.NotificationDeadline != null
                    && b.NotificationDeadline < DateTime.UtcNow
                    && !b.DpaNotified)
                .OrderBy(b => b.NotificationDeadline)
                .ToListAsync();
            return Results.Ok(overdue);
        }).WithName("ListOverdueBreaches");
    }

    // ─── Compliance Training ────────────────────────────────────────────

    private static void MapComplianceTraining(RouteGroupBuilder group)
    {
        group.MapGet("/training", async (IvfDbContext db) =>
        {
            var trainings = await db.ComplianceTrainings
                .Where(t => !t.IsDeleted)
                .OrderByDescending(t => t.AssignedAt)
                .ToListAsync();
            return Results.Ok(trainings);
        }).WithName("ListTrainings");

        group.MapGet("/training/user/{userId:guid}", async (Guid userId, IvfDbContext db) =>
        {
            var trainings = await db.ComplianceTrainings
                .Where(t => t.UserId == userId && !t.IsDeleted)
                .OrderByDescending(t => t.AssignedAt)
                .ToListAsync();
            return Results.Ok(trainings);
        }).WithName("GetUserTrainings");

        group.MapPost("/training/assign", async (AssignTrainingRequest req, IvfDbContext db, HttpContext ctx) =>
        {
            var adminId = Guid.Parse(ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst("userId")?.Value ?? Guid.Empty.ToString());

            var training = ComplianceTraining.Assign(
                req.UserId,
                req.TrainingType,
                req.TrainingName,
                req.DueDate,
                adminId,
                req.Description,
                req.PassThreshold,
                req.ExpiresAt);

            db.ComplianceTrainings.Add(training);
            await db.SaveChangesAsync();
            return Results.Created($"/api/compliance/training/{training.Id}", training);
        }).WithName("AssignTraining");

        group.MapPost("/training/assign-bulk", async (BulkAssignTrainingRequest req, IvfDbContext db, HttpContext ctx) =>
        {
            var adminId = Guid.Parse(ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst("userId")?.Value ?? Guid.Empty.ToString());
            var created = new List<ComplianceTraining>();

            foreach (var userId in req.UserIds)
            {
                var training = ComplianceTraining.Assign(
                    userId,
                    req.TrainingType,
                    req.TrainingName,
                    req.DueDate,
                    adminId,
                    req.Description,
                    req.PassThreshold,
                    req.ExpiresAt);
                db.ComplianceTrainings.Add(training);
                created.Add(training);
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { assigned = created.Count, trainings = created.Select(t => t.Id) });
        }).WithName("BulkAssignTraining");

        group.MapPost("/training/{id:guid}/complete", async (Guid id, CompleteTrainingRequest req, IvfDbContext db) =>
        {
            var training = await db.ComplianceTrainings.FindAsync(id);
            if (training is null) return Results.NotFound();

            training.Complete(req.ScorePercent, req.CertificateId, req.Evidence);
            await db.SaveChangesAsync();
            return Results.Ok(training);
        }).WithName("CompleteTraining");

        group.MapGet("/training/overdue", async (IvfDbContext db) =>
        {
            var overdue = await db.ComplianceTrainings
                .Where(t => !t.IsDeleted && !t.IsCompleted && t.DueDate < DateTime.UtcNow)
                .OrderBy(t => t.DueDate)
                .ToListAsync();
            return Results.Ok(overdue);
        }).WithName("ListOverdueTrainings");

        group.MapGet("/training/expiring", async (IvfDbContext db) =>
        {
            var thirtyDaysFromNow = DateTime.UtcNow.AddDays(30);
            var expiring = await db.ComplianceTrainings
                .Where(t => !t.IsDeleted && t.IsCompleted
                    && t.ExpiresAt != null && t.ExpiresAt < thirtyDaysFromNow)
                .OrderBy(t => t.ExpiresAt)
                .ToListAsync();
            return Results.Ok(expiring);
        }).WithName("ListExpiringTrainings");

        group.MapGet("/training/summary", async (IvfDbContext db) =>
        {
            var all = await db.ComplianceTrainings
                .Where(t => !t.IsDeleted)
                .ToListAsync();

            var summary = new
            {
                total = all.Count,
                completed = all.Count(t => t.IsCompleted),
                passed = all.Count(t => t.IsPassed),
                overdue = all.Count(t => t.IsOverdue()),
                expiring = all.Count(t => t.NeedsRenewal()),
                completionRate = all.Count > 0 ? Math.Round(all.Count(t => t.IsCompleted) * 100.0 / all.Count, 1) : 0,
                passRate = all.Count(t => t.IsCompleted) > 0
                    ? Math.Round(all.Count(t => t.IsPassed) * 100.0 / all.Count(t => t.IsCompleted), 1) : 0,
                byType = all.GroupBy(t => t.TrainingType).Select(g => new
                {
                    type = g.Key,
                    total = g.Count(),
                    completed = g.Count(t => t.IsCompleted),
                    passed = g.Count(t => t.IsPassed)
                })
            };
            return Results.Ok(summary);
        }).WithName("TrainingSummary");
    }

    // ─── Password Policy ────────────────────────────────────────────────

    private static void MapPasswordPolicy(RouteGroupBuilder group)
    {
        group.MapPost("/password/validate", (ValidatePasswordRequest req, PasswordPolicyService svc) =>
        {
            var result = svc.Validate(req.Password, req.Username);
            return Results.Ok(result);
        }).WithName("ValidatePassword")
          .AllowAnonymous();

        group.MapGet("/password/policy", () =>
        {
            return Results.Ok(new
            {
                minLength = 10,
                maxLength = 128,
                requiredCategories = 3,
                categories = new[] { "lowercase", "uppercase", "digits", "special_characters" },
                bannedPasswordCheck = true,
                usernameCheck = true,
                repetitivePatternCheck = true,
                sequentialPatternCheck = true,
                entropyScoring = true,
                standard = "NIST SP 800-63B (2024)",
                additionalStandards = new[] { "Google Workspace", "Microsoft Entra ID", "AWS IAM" }
            });
        }).WithName("GetPasswordPolicy")
          .AllowAnonymous();
    }

    // ─── Compliance Dashboard ───────────────────────────────────────────

    private static void MapComplianceDashboard(RouteGroupBuilder group)
    {
        group.MapGet("/dashboard", async (
            IvfDbContext db,
            Application.Common.Interfaces.IComplianceScoringEngine complianceEngine) =>
        {
            // Compliance scoring
            var complianceReport = await complianceEngine.EvaluateAsync();

            // Breach summary
            var breaches = await db.BreachNotifications
                .Where(b => !b.IsDeleted)
                .ToListAsync();
            var activeBreaches = breaches.Where(b => b.Status != "Resolved" && b.Status != "Closed").ToList();

            // Training summary
            var trainings = await db.ComplianceTrainings
                .Where(t => !t.IsDeleted)
                .ToListAsync();

            // Data retention
            var retentionPolicies = await db.DataRetentionPolicies
                .Where(p => !p.IsDeleted && p.IsEnabled)
                .ToListAsync();

            // Consent stats
            var consents = await db.UserConsents
                .Where(c => !c.IsDeleted)
                .ToListAsync();

            // Incident stats
            var incidents = await db.SecurityIncidents
                .Where(i => !i.IsDeleted)
                .ToListAsync();
            var openIncidents = incidents.Where(i => i.Status == "Open" || i.Status == "Investigating").ToList();

            // Phase 2: Asset inventory stats
            var assets = await db.AssetInventories
                .Where(a => !a.IsDeleted && a.Status == AssetStatus.Active)
                .ToListAsync();

            // Phase 2: ROPA stats
            var processingActivities = await db.ProcessingActivities
                .Where(p => !p.IsDeleted)
                .ToListAsync();

            // Phase 2: AI bias test stats
            var biasTests = await db.AiBiasTestResults
                .Where(t => !t.IsDeleted)
                .ToListAsync();
            var latestBiasTests = biasTests
                .GroupBy(t => new { t.AiSystemName, t.ProtectedAttribute, t.ProtectedGroupValue })
                .Select(g => g.OrderByDescending(t => t.TestRunAt).First())
                .ToList();

            var dashboard = new
            {
                evaluatedAt = DateTime.UtcNow,
                compliance = new
                {
                    overallScore = complianceReport.OverallScore,
                    maxScore = complianceReport.MaxScore,
                    percentage = complianceReport.Percentage,
                    grade = complianceReport.Grade,
                    frameworks = complianceReport.Frameworks.Select(f => new
                    {
                        name = f.Name,
                        score = f.Score,
                        maxScore = f.MaxScore,
                        percentage = f.Percentage,
                        passedControls = f.Controls.Count(c => c.Status == Application.Common.Interfaces.ControlStatus.Pass),
                        failedControls = f.Controls.Count(c => c.Status == Application.Common.Interfaces.ControlStatus.Fail),
                        partialControls = f.Controls.Count(c => c.Status == Application.Common.Interfaces.ControlStatus.Partial)
                    })
                },
                breaches = new
                {
                    total = breaches.Count,
                    active = activeBreaches.Count,
                    overdue = activeBreaches.Count(b => b.IsDeadlineAtRisk()),
                    requireHhs = activeBreaches.Count(b => b.RequiresHhsNotification() && !b.HhsNotified),
                    bySeverity = breaches.GroupBy(b => b.Severity)
                        .Select(g => new { severity = g.Key, count = g.Count() })
                },
                training = new
                {
                    totalAssigned = trainings.Count,
                    completed = trainings.Count(t => t.IsCompleted),
                    passed = trainings.Count(t => t.IsPassed),
                    overdue = trainings.Count(t => t.IsOverdue()),
                    needsRenewal = trainings.Count(t => t.NeedsRenewal()),
                    completionRate = trainings.Count > 0
                        ? Math.Round(trainings.Count(t => t.IsCompleted) * 100.0 / trainings.Count, 1) : 0
                },
                dataRetention = new
                {
                    activePolicies = retentionPolicies.Count,
                    lastExecution = retentionPolicies
                        .Where(p => p.LastExecutedAt.HasValue)
                        .OrderByDescending(p => p.LastExecutedAt)
                        .Select(p => p.LastExecutedAt)
                        .FirstOrDefault()
                },
                consent = new
                {
                    totalConsents = consents.Count,
                    activeConsents = consents.Count(c => c.IsValid()),
                    revokedConsents = consents.Count(c => !c.IsGranted),
                    byType = consents.GroupBy(c => c.ConsentType)
                        .Select(g => new
                        {
                            type = g.Key,
                            granted = g.Count(c => c.IsValid()),
                            revoked = g.Count(c => !c.IsGranted)
                        })
                },
                incidents = new
                {
                    total = incidents.Count,
                    open = openIncidents.Count,
                    bySeverity = incidents.GroupBy(i => i.Severity)
                        .Select(g => new { severity = g.Key, count = g.Count() })
                },
                assetInventory = new
                {
                    totalAssets = assets.Count,
                    containingPhi = assets.Count(a => a.ContainsPhi),
                    containingPii = assets.Count(a => a.ContainsPii),
                    overdueForAudit = assets.Count(a => a.IsOverdueForAudit()),
                    averageRiskScore = assets.Count > 0
                        ? Math.Round(assets.Average(a => a.CalculateRiskScore()), 1) : 0,
                    highRiskCount = assets.Count(a => a.CalculateRiskScore() >= 50)
                },
                ropa = new
                {
                    totalActivities = processingActivities.Count,
                    active = processingActivities.Count(p => p.Status == ProcessingActivityStatus.Active),
                    dpiaPending = processingActivities.Count(p => p.RequiresDpia && !p.DpiaCompleted),
                    overdueForReview = processingActivities.Count(p => p.IsOverdueForReview())
                },
                aiFairness = new
                {
                    totalTestsRun = biasTests.Count,
                    aiSystemsTested = latestBiasTests.Select(t => t.AiSystemName).Distinct().Count(),
                    overallFairnessRate = latestBiasTests.Count > 0
                        ? Math.Round(latestBiasTests.Count(t => t.PassesFairnessThreshold) * 100.0 / latestBiasTests.Count, 1) : 0,
                    failingGroups = latestBiasTests.Count(t => !t.PassesFairnessThreshold),
                    lastTestDate = biasTests.Count > 0 ? biasTests.Max(t => t.TestRunAt) : (DateTime?)null
                }
            };

            return Results.Ok(dashboard);
        }).WithName("ComplianceDashboard");
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────

public record CreateBreachRequest(
    Guid IncidentId,
    string BreachType,
    string Severity,
    int AffectedRecordCount,
    string[]? AffectedDataTypes = null,
    string[]? AffectedSystems = null,
    string? RootCause = null,
    string? AttackVector = null);

public record AssessBreachRequest(
    int AffectedRecordCount,
    string[]? AffectedDataTypes = null,
    Guid[]? AffectedUserIds = null);

public record ContainBreachRequest(string[]? RemediationSteps);

public record NotifyDpaRequest(string? Reference);

public record NotifySubjectsRequest(int Count);

public record ResolveBreachRequest(
    string? LessonsLearned,
    string[]? PreventionMeasures = null);

public record AssignTrainingRequest(
    Guid UserId,
    string TrainingType,
    string TrainingName,
    DateTime DueDate,
    string? Description = null,
    int PassThreshold = 80,
    DateTime? ExpiresAt = null);

public record BulkAssignTrainingRequest(
    Guid[] UserIds,
    string TrainingType,
    string TrainingName,
    DateTime DueDate,
    string? Description = null,
    int PassThreshold = 80,
    DateTime? ExpiresAt = null);

public record CompleteTrainingRequest(
    int ScorePercent,
    string? CertificateId = null,
    string? Evidence = null);

public record ValidatePasswordRequest(
    string Password,
    string? Username = null);
