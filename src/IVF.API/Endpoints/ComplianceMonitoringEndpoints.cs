using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class ComplianceMonitoringEndpoints
{
    public static void MapComplianceMonitoringEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/compliance/monitoring")
            .WithTags("Compliance Monitoring (Phase 4)")
            .RequireAuthorization("AdminOnly");

        // Unified compliance health dashboard
        group.MapGet("/health", async (IvfDbContext db) =>
        {
            var breaches = await db.BreachNotifications.Where(b => !b.IsDeleted).ToListAsync();
            var dsrs = await db.DataSubjectRequests.Where(r => !r.IsDeleted).ToListAsync();
            var schedule = await db.ComplianceSchedules.Where(s => !s.IsDeleted && s.Status == ScheduleStatus.Active).ToListAsync();
            var incidents = await db.SecurityIncidents.Where(i => !i.IsDeleted).ToListAsync();
            var trainings = await db.ComplianceTrainings.Where(t => !t.IsDeleted).ToListAsync();
            var assets = await db.AssetInventories.Where(a => !a.IsDeleted).ToListAsync();
            var processingActivities = await db.ProcessingActivities.Where(p => !p.IsDeleted).ToListAsync();
            var biasTests = await db.AiBiasTestResults.Where(b => !b.IsDeleted).ToListAsync();
            var modelVersions = await db.AiModelVersions.Where(m => !m.IsDeleted).ToListAsync();

            var overdueDsrs = dsrs.Count(r => r.IsOverdue);
            var overdueTasks = schedule.Count(s => s.IsOverdue);
            var openIncidents = incidents.Count(i => i.Status != "Resolved" && i.Status != "Closed");
            var trainingCompliance = trainings.Count == 0 ? 100.0 :
                Math.Round((double)trainings.Count(t => t.IsCompleted) / trainings.Count * 100, 1);

            var healthScore = CalculateHealthScore(overdueDsrs, overdueTasks, openIncidents, trainingCompliance);

            return Results.Ok(new
            {
                overallHealthScore = healthScore,
                healthStatus = healthScore >= 90 ? "Healthy" : healthScore >= 70 ? "Warning" : "Critical",
                gdpr = new
                {
                    dsrTotal = dsrs.Count,
                    dsrPending = dsrs.Count(r => r.Status != DsrStatus.Completed && r.Status != DsrStatus.Rejected),
                    dsrOverdue = overdueDsrs,
                    dsrComplianceRate = dsrs.Count == 0 ? 100.0 :
                        Math.Round((double)dsrs.Count(r => !r.IsOverdue) / dsrs.Count * 100, 1),
                    ropaActivities = processingActivities.Count,
                    breachesLast90Days = breaches.Count(b => b.CreatedAt >= DateTime.UtcNow.AddDays(-90))
                },
                securityOperations = new
                {
                    openIncidents,
                    criticalIncidents = incidents.Count(i => i.Severity == "Critical" && i.Status != "Resolved" && i.Status != "Closed"),
                    breachesTotal = breaches.Count,
                    unresolvedBreaches = breaches.Count(b => b.Status != "Resolved" && b.Status != "Closed")
                },
                complianceTasks = new
                {
                    activeTasks = schedule.Count,
                    overdueTasks,
                    upcomingTasks = schedule.Count(s => s.IsUpcoming),
                    completionRate = schedule.Count == 0 ? 100.0 :
                        Math.Round((double)schedule.Count(s => !s.IsOverdue) / schedule.Count * 100, 1)
                },
                training = new
                {
                    totalPrograms = trainings.Count,
                    completed = trainings.Count(t => t.IsCompleted),
                    complianceRate = trainingCompliance
                },
                assetManagement = new
                {
                    totalAssets = assets.Count,
                    assetsWithOwner = assets.Count(a => !string.IsNullOrEmpty(a.Owner))
                },
                aiGovernance = new
                {
                    totalModels = modelVersions.Select(m => m.AiSystemName).Distinct().Count(),
                    deployedVersions = modelVersions.Count(m => m.Status == ModelVersionStatus.Deployed),
                    biasTestsPassed = biasTests.Count(b => b.PassesFairnessThreshold),
                    biasTestsFailed = biasTests.Count(b => !b.PassesFairnessThreshold),
                    lastBiasTest = biasTests.OrderByDescending(b => b.CreatedAt).FirstOrDefault()?.CreatedAt
                },
                alerts = BuildAlerts(overdueDsrs, overdueTasks, openIncidents, trainingCompliance, biasTests)
            });
        });

        // Continuous monitoring — security event trends
        group.MapGet("/security-trends", async (
            [FromQuery] int days,
            IvfDbContext db) =>
        {
            if (days < 1 || days > 365) days = 30;
            var since = DateTime.UtcNow.AddDays(-days);

            var events = await db.SecurityEvents
                .Where(e => e.CreatedAt >= since && !e.IsDeleted)
                .GroupBy(e => e.CreatedAt.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .OrderBy(g => g.date)
                .ToListAsync();

            var incidents = await db.SecurityIncidents
                .Where(i => i.CreatedAt >= since && !i.IsDeleted)
                .GroupBy(i => i.CreatedAt.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .OrderBy(g => g.date)
                .ToListAsync();

            return Results.Ok(new { period = days, securityEvents = events, securityIncidents = incidents });
        });

        // AI model performance monitoring — post-deployment alerts
        group.MapGet("/ai-performance", async (IvfDbContext db) =>
        {
            var models = await db.AiModelVersions
                .Where(m => !m.IsDeleted && m.Status == ModelVersionStatus.Deployed)
                .ToListAsync();

            var alerts = new List<object>();
            foreach (var model in models)
            {
                if (model.Accuracy.HasValue && model.Accuracy < 0.95)
                    alerts.Add(new { model.AiSystemName, model.ModelVersion, metric = "Accuracy", value = model.Accuracy, threshold = 0.95, severity = "Warning" });
                if (model.Fpr.HasValue && model.Fpr > 0.01)
                    alerts.Add(new { model.AiSystemName, model.ModelVersion, metric = "FPR", value = model.Fpr, threshold = 0.01, severity = "Warning" });
                if (model.Fnr.HasValue && model.Fnr > 0.05)
                    alerts.Add(new { model.AiSystemName, model.ModelVersion, metric = "FNR", value = model.Fnr, threshold = 0.05, severity = "Critical" });
                if (!model.BiasTestPassed)
                    alerts.Add(new { model.AiSystemName, model.ModelVersion, metric = "BiasTest", value = (double?)0, threshold = 1.0, severity = "Critical" });
            }

            return Results.Ok(new
            {
                deployedModels = models.Select(m => new
                {
                    m.AiSystemName,
                    m.ModelVersion,
                    m.Accuracy,
                    m.Precision,
                    m.Recall,
                    m.F1Score,
                    m.Fpr,
                    m.Fnr,
                    m.BiasTestPassed,
                    m.DeployedAt
                }),
                alerts,
                alertCount = alerts.Count,
                status = alerts.Any(a => ((dynamic)a).severity == "Critical") ? "Critical" :
                         alerts.Count != 0 ? "Warning" : "Healthy"
            });
        });

        // Audit readiness check — verify all evidence is current
        group.MapGet("/audit-readiness", async (IvfDbContext db) =>
        {
            var schedule = await db.ComplianceSchedules
                .Where(s => !s.IsDeleted && s.Status == ScheduleStatus.Active)
                .ToListAsync();

            var dsrs = await db.DataSubjectRequests.Where(r => !r.IsDeleted).ToListAsync();
            var trainings = await db.ComplianceTrainings.Where(t => !t.IsDeleted).ToListAsync();

            var frameworks = new[] { "SOC2", "ISO27001", "HIPAA", "GDPR", "HITRUST", "NIST_AI_RMF", "ISO42001" };
            var readiness = frameworks.Select(fw =>
            {
                var fwTasks = schedule.Where(s => s.Framework == fw || s.Framework == "ALL").ToList();
                var overdue = fwTasks.Count(s => s.IsOverdue);
                var total = fwTasks.Count;
                return new
                {
                    framework = fw,
                    totalTasks = total,
                    completedOnTime = total - overdue,
                    overdue,
                    readinessScore = total == 0 ? 100.0 : Math.Round((double)(total - overdue) / total * 100, 1)
                };
            });

            return Results.Ok(new
            {
                readinessByFramework = readiness,
                overallReadiness = Math.Round(readiness.Average(r => r.readinessScore), 1),
                dsrCompliance = new
                {
                    totalDsrs = dsrs.Count,
                    completedWithinDeadline = dsrs.Count(r => r.CompletedAt.HasValue && r.CompletedAt <= (r.ExtendedDeadline ?? r.Deadline)),
                    overdue = dsrs.Count(r => r.IsOverdue)
                },
                trainingCompliance = new
                {
                    totalPrograms = trainings.Count,
                    completed = trainings.Count(t => t.IsCompleted),
                    rate = trainings.Count == 0 ? 100.0 :
                        Math.Round((double)trainings.Count(t => t.IsCompleted) / trainings.Count * 100, 1)
                }
            });
        });
    }

    private static double CalculateHealthScore(int overdueDsrs, int overdueTasks, int openIncidents, double trainingCompliance)
    {
        var score = 100.0;
        score -= overdueDsrs * 5;     // Each overdue DSR costs 5 points
        score -= overdueTasks * 3;    // Each overdue task costs 3 points
        score -= openIncidents * 4;   // Each open incident costs 4 points
        score -= (100 - trainingCompliance) * 0.2; // Training gap impact
        return Math.Max(0, Math.Round(score, 1));
    }

    private static List<object> BuildAlerts(int overdueDsrs, int overdueTasks, int openIncidents, double trainingRate, List<AiBiasTestResult> biasTests)
    {
        var alerts = new List<object>();
        if (overdueDsrs > 0)
            alerts.Add(new { type = "DSR_OVERDUE", severity = "Critical", message = $"{overdueDsrs} data subject request(s) past deadline", framework = "GDPR" });
        if (overdueTasks > 0)
            alerts.Add(new { type = "TASK_OVERDUE", severity = "Warning", message = $"{overdueTasks} compliance task(s) overdue", framework = "ALL" });
        if (openIncidents > 0)
            alerts.Add(new { type = "OPEN_INCIDENTS", severity = openIncidents > 3 ? "Critical" : "Warning", message = $"{openIncidents} unresolved security incident(s)", framework = "SOC2" });
        if (trainingRate < 80)
            alerts.Add(new { type = "TRAINING_GAP", severity = "Warning", message = $"Training compliance at {trainingRate}% (target: 80%)", framework = "HIPAA" });
        var failedBias = biasTests.Count(b => !b.PassesFairnessThreshold && b.CreatedAt >= DateTime.UtcNow.AddDays(-90));
        if (failedBias > 0)
            alerts.Add(new { type = "BIAS_TEST_FAILED", severity = "Critical", message = $"{failedBias} AI bias test(s) failed in last 90 days", framework = "NIST_AI_RMF" });
        return alerts;
    }
}
