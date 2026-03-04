using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class AiBiasEndpoints
{
    public static void MapAiBiasEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai")
            .WithTags("AI Governance")
            .RequireAuthorization("AdminOnly");

        MapBiasTests(group);
        MapFprFnrDashboard(group);
        MapExplainability(group);
    }

    // ─── AI Bias Test Results ───────────────────────────────────────────

    private static void MapBiasTests(RouteGroupBuilder group)
    {
        // List all bias test results
        group.MapGet("/bias-tests", async (
            IvfDbContext db,
            string? aiSystem,
            string? testType,
            string? protectedAttribute) =>
        {
            var query = db.AiBiasTestResults.Where(t => !t.IsDeleted).AsQueryable();

            if (!string.IsNullOrEmpty(aiSystem))
                query = query.Where(t => t.AiSystemName == aiSystem);
            if (!string.IsNullOrEmpty(testType))
                query = query.Where(t => t.TestType == testType);
            if (!string.IsNullOrEmpty(protectedAttribute))
                query = query.Where(t => t.ProtectedAttribute == protectedAttribute);

            var results = await query.OrderByDescending(t => t.TestRunAt).ToListAsync();
            return Results.Ok(results);
        }).WithName("ListBiasTests");

        // Get single test result
        group.MapGet("/bias-tests/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var result = await db.AiBiasTestResults.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName("GetBiasTest");

        // Record a bias test result
        group.MapPost("/bias-tests", async (CreateBiasTestRequest req, IvfDbContext db) =>
        {
            var result = AiBiasTestResult.Create(
                req.AiSystemName, req.TestType,
                req.ProtectedAttribute, req.ProtectedGroupValue,
                req.SampleSize,
                req.TruePositives, req.FalsePositives,
                req.TrueNegatives, req.FalseNegatives,
                req.BaselineFpr, req.BaselineFnr,
                req.TestPeriodStart, req.TestPeriodEnd,
                req.TestRunBy, req.FairnessThreshold);

            if (!string.IsNullOrEmpty(req.Explanation))
                result.SetExplanation(req.Explanation, req.FeatureImportance);

            db.AiBiasTestResults.Add(result);
            await db.SaveChangesAsync();
            return Results.Created($"/api/ai/bias-tests/{result.Id}", result);
        }).WithName("CreateBiasTest");

        // Add explanation to existing test
        group.MapPut("/bias-tests/{id:guid}/explanation", async (
            Guid id, SetExplanationRequest req, IvfDbContext db) =>
        {
            var result = await db.AiBiasTestResults.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
            if (result is null) return Results.NotFound();
            result.SetExplanation(req.Explanation, req.FeatureImportance);
            await db.SaveChangesAsync();
            return Results.Ok(result);
        }).WithName("SetBiasTestExplanation");

        // Add remediation to existing test
        group.MapPut("/bias-tests/{id:guid}/remediation", async (
            Guid id, SetRemediationRequest req, IvfDbContext db) =>
        {
            var result = await db.AiBiasTestResults.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
            if (result is null) return Results.NotFound();
            result.SetRemediation(req.Remediation);
            await db.SaveChangesAsync();
            return Results.Ok(result);
        }).WithName("SetBiasTestRemediation");

        // Bias test summary by AI system
        group.MapGet("/bias-tests/summary", async (IvfDbContext db) =>
        {
            var tests = await db.AiBiasTestResults
                .Where(t => !t.IsDeleted)
                .ToListAsync();

            var summary = tests
                .GroupBy(t => t.AiSystemName)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(t => t.TestRunAt).ToList();
                    var latestByAttribute = latest
                        .GroupBy(t => new { t.ProtectedAttribute, t.ProtectedGroupValue })
                        .Select(ag => ag.First())
                        .ToList();

                    return new
                    {
                        aiSystem = g.Key,
                        totalTests = g.Count(),
                        latestTestDate = latest.First().TestRunAt,
                        passingTests = latestByAttribute.Count(t => t.PassesFairnessThreshold),
                        failingTests = latestByAttribute.Count(t => !t.PassesFairnessThreshold),
                        overallFairness = latestByAttribute.Count > 0
                            ? Math.Round(latestByAttribute.Count(t => t.PassesFairnessThreshold) * 100.0 / latestByAttribute.Count, 1)
                            : 0,
                        avgFpr = Math.Round(latestByAttribute.Average(t => (double)t.FalsePositiveRate), 6),
                        avgFnr = Math.Round(latestByAttribute.Average(t => (double)t.FalseNegativeRate), 6),
                        maxDisparityFpr = Math.Round((double)latestByAttribute.Max(t => Math.Abs(t.DisparityRatioFpr - 1)), 4),
                        maxDisparityFnr = Math.Round((double)latestByAttribute.Max(t => Math.Abs(t.DisparityRatioFnr - 1)), 4),
                        protectedAttributes = latestByAttribute
                            .GroupBy(t => t.ProtectedAttribute)
                            .Select(ag => new
                            {
                                attribute = ag.Key,
                                groupsTested = ag.Count(),
                                passing = ag.Count(t => t.PassesFairnessThreshold),
                                failing = ag.Count(t => !t.PassesFairnessThreshold)
                            })
                    };
                });

            return Results.Ok(summary);
        }).WithName("BiasTestSummary");
    }

    // ─── FPR/FNR Tracking Dashboard ─────────────────────────────────────

    private static void MapFprFnrDashboard(RouteGroupBuilder group)
    {
        // Per-system FPR/FNR trends over time
        group.MapGet("/fpr-fnr", async (IvfDbContext db, string? aiSystem, int? lastDays) =>
        {
            var cutoff = DateTime.UtcNow.AddDays(-(lastDays ?? 90));

            var query = db.AiBiasTestResults
                .Where(t => !t.IsDeleted && t.TestRunAt >= cutoff);

            if (!string.IsNullOrEmpty(aiSystem))
                query = query.Where(t => t.AiSystemName == aiSystem);

            var tests = await query.OrderBy(t => t.TestRunAt).ToListAsync();

            var trends = tests
                .GroupBy(t => t.AiSystemName)
                .Select(g => new
                {
                    aiSystem = g.Key,
                    dataPoints = g
                        .GroupBy(t => t.TestRunAt.Date)
                        .Select(dg => new
                        {
                            date = dg.Key,
                            avgFpr = Math.Round(dg.Average(t => (double)t.FalsePositiveRate), 6),
                            avgFnr = Math.Round(dg.Average(t => (double)t.FalseNegativeRate), 6),
                            avgAccuracy = Math.Round(dg.Average(t => (double)t.Accuracy), 6),
                            avgPrecision = Math.Round(dg.Average(t => (double)t.Precision), 6),
                            avgRecall = Math.Round(dg.Average(t => (double)t.Recall), 6),
                            avgF1 = Math.Round(dg.Average(t => (double)t.F1Score), 6),
                            sampleSize = dg.Sum(t => t.SampleSize),
                            testsRun = dg.Count()
                        })
                        .OrderBy(d => d.date)
                });

            return Results.Ok(trends);
        }).WithName("FprFnrTrends");

        // Comprehensive AI governance dashboard
        group.MapGet("/dashboard", async (IvfDbContext db) =>
        {
            var tests = await db.AiBiasTestResults
                .Where(t => !t.IsDeleted)
                .ToListAsync();

            var latestTests = tests
                .GroupBy(t => new { t.AiSystemName, t.ProtectedAttribute, t.ProtectedGroupValue })
                .Select(g => g.OrderByDescending(t => t.TestRunAt).First())
                .ToList();

            var dashboard = new
            {
                evaluatedAt = DateTime.UtcNow,
                totalTestsRecorded = tests.Count,
                aiSystems = latestTests
                    .GroupBy(t => t.AiSystemName)
                    .Select(g => new
                    {
                        name = g.Key,
                        overallFairnessScore = g.Count() > 0
                            ? Math.Round(g.Count(t => t.PassesFairnessThreshold) * 100.0 / g.Count(), 1) : 0,
                        avgAccuracy = Math.Round(g.Average(t => (double)t.Accuracy) * 100, 2),
                        avgFpr = Math.Round(g.Average(t => (double)t.FalsePositiveRate) * 100, 4),
                        avgFnr = Math.Round(g.Average(t => (double)t.FalseNegativeRate) * 100, 4),
                        avgPrecision = Math.Round(g.Average(t => (double)t.Precision) * 100, 2),
                        avgRecall = Math.Round(g.Average(t => (double)t.Recall) * 100, 2),
                        avgF1 = Math.Round(g.Average(t => (double)t.F1Score) * 100, 2),
                        passingGroups = g.Count(t => t.PassesFairnessThreshold),
                        failingGroups = g.Count(t => !t.PassesFairnessThreshold),
                        totalSamples = g.Sum(t => t.SampleSize),
                        lastTestDate = g.Max(t => t.TestRunAt)
                    }),
                overallMetrics = new
                {
                    totalAiSystems = latestTests.Select(t => t.AiSystemName).Distinct().Count(),
                    totalGroupsTested = latestTests.Count,
                    overallFairnessRate = latestTests.Count > 0
                        ? Math.Round(latestTests.Count(t => t.PassesFairnessThreshold) * 100.0 / latestTests.Count, 1) : 0,
                    systemsWithBias = latestTests
                        .GroupBy(t => t.AiSystemName)
                        .Count(g => g.Any(t => !t.PassesFairnessThreshold)),
                    systemsFullyFair = latestTests
                        .GroupBy(t => t.AiSystemName)
                        .Count(g => g.All(t => t.PassesFairnessThreshold))
                },
                biasAlerts = latestTests
                    .Where(t => !t.PassesFairnessThreshold)
                    .Select(t => new
                    {
                        t.AiSystemName,
                        t.ProtectedAttribute,
                        t.ProtectedGroupValue,
                        fprDisparity = t.DisparityRatioFpr,
                        fnrDisparity = t.DisparityRatioFnr,
                        t.Explanation,
                        t.RemediationAction,
                        t.TestRunAt
                    })
                    .OrderByDescending(t => Math.Max(
                        Math.Abs((double)t.fprDisparity - 1),
                        Math.Abs((double)t.fnrDisparity - 1)))
            };

            return Results.Ok(dashboard);
        }).WithName("AiGovernanceDashboard");
    }

    // ─── User Explainability ────────────────────────────────────────────

    private static void MapExplainability(RouteGroupBuilder group)
    {
        // Get explanation for a specific security event
        group.MapGet("/explain/{eventId:guid}", async (Guid eventId, IvfDbContext db) =>
        {
            var securityEvent = await db.SecurityEvents
                .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted);

            if (securityEvent is null)
                return Results.NotFound(new { message = "Security event not found" });

            var explanation = new
            {
                eventId = securityEvent.Id,
                eventType = securityEvent.EventType,
                severity = securityEvent.Severity,
                occurredAt = securityEvent.CreatedAt,
                decision = new
                {
                    action = securityEvent.IsBlocked ? "Blocked" : "Allowed",
                    reason = GetEventTypeExplanation(securityEvent.EventType),
                    riskScore = securityEvent.RiskScore,
                    riskLevel = securityEvent.RiskScore switch
                    {
                        >= 80 => "Critical",
                        >= 60 => "High",
                        >= 40 => "Medium",
                        >= 20 => "Low",
                        _ => "Info"
                    }
                },
                context = new
                {
                    ipAddress = securityEvent.IpAddress,
                    country = securityEvent.Country,
                    requestPath = securityEvent.RequestPath,
                    correlationId = securityEvent.CorrelationId
                },
                factorsConsidered = GetThreatFactors(securityEvent),
                userRecourse = new
                {
                    canAppeal = securityEvent.IsBlocked,
                    appealProcess = securityEvent.IsBlocked
                        ? "Contact your administrator with this event ID to request a review. " +
                          "If this was a false positive, the block will be lifted and the AI model parameters adjusted."
                        : null,
                    referenceId = securityEvent.CorrelationId ?? securityEvent.Id.ToString()
                },
                transparency = new
                {
                    aiSystem = DetermineAiSystem(securityEvent.EventType),
                    modelVersion = "v3.0",
                    decisionExplainability = "This decision was made by an automated security system. " +
                        "All automated decisions can be reviewed and overridden by an authorized administrator.",
                    regulatoryBasis = "NIST AI RMF MAP 3, ISO 42001 Annex A.10, GDPR Art. 22"
                }
            };

            return Results.Ok(explanation);
        }).WithName("ExplainSecurityEvent");

        // Get explainability for a user's recent blocked events
        group.MapGet("/explain/user/{userId:guid}", async (Guid userId, IvfDbContext db, int? limit) =>
        {
            var events = await db.SecurityEvents
                .Where(e => e.UserId == userId && !e.IsDeleted && e.IsBlocked)
                .OrderByDescending(e => e.CreatedAt)
                .Take(limit ?? 10)
                .ToListAsync();

            var explanations = events.Select(e => new
            {
                eventId = e.Id,
                eventType = e.EventType,
                occurredAt = e.CreatedAt,
                action = "Blocked",
                reason = GetEventTypeExplanation(e.EventType),
                riskScore = e.RiskScore,
                canAppeal = true
            });

            return Results.Ok(new
            {
                userId,
                blockedEvents = explanations,
                totalBlocked = events.Count,
                appealInstructions = "To appeal any blocked event, contact your administrator " +
                    "with the event ID. All automated blocks are subject to human review."
            });
        }).WithName("ExplainUserBlockedEvents");

        // AI systems transparency report
        group.MapGet("/transparency", async (IvfDbContext db) =>
        {
            var recentEvents = await db.SecurityEvents
                .Where(e => !e.IsDeleted && e.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                .ToListAsync();

            var biasTests = await db.AiBiasTestResults
                .Where(t => !t.IsDeleted)
                .GroupBy(t => t.AiSystemName)
                .Select(g => new
                {
                    System = g.Key,
                    LastTest = g.Max(t => t.TestRunAt),
                    LatestResults = g.OrderByDescending(t => t.TestRunAt).Take(5).ToList()
                })
                .ToListAsync();

            var systems = new[]
            {
                new { name = AiSystemNames.ThreatDetection,
                      purpose = "Identifies security threats using 7 signal categories (Tor, VPN, impossible travel, brute force, anomalous access, injection, off-hours)",
                      type = "Rule-based + Statistical", riskLevel = "Medium", humanOverride = true },
                new { name = AiSystemNames.BehavioralAnalytics,
                      purpose = "Detects anomalous user behavior using z-score analysis on 30-day baselines",
                      type = "Statistical ML", riskLevel = "Medium", humanOverride = true },
                new { name = AiSystemNames.BiometricMatcher,
                      purpose = "Fingerprint identification via DigitalPersona SDK for patient identification",
                      type = "Third-party ML (template matching)", riskLevel = "High", humanOverride = true },
                new { name = AiSystemNames.BotDetection,
                      purpose = "Automated bot/scanner detection via user agent inspection + reCAPTCHA",
                      type = "Rule-based + External API", riskLevel = "Low", humanOverride = true },
                new { name = AiSystemNames.ContextualAuth,
                      purpose = "Risk-based adaptive authentication with step-up MFA based on context signals",
                      type = "Rule-based Scoring", riskLevel = "Medium", humanOverride = true }
            };

            var report = new
            {
                generatedAt = DateTime.UtcNow,
                reportingPeriod = new { start = DateTime.UtcNow.AddDays(-30), end = DateTime.UtcNow },
                aiSystems = systems.Select(s => new
                {
                    s.name,
                    s.purpose,
                    s.type,
                    s.riskLevel,
                    s.humanOverride,
                    last30DaysActivity = new
                    {
                        totalEvents = recentEvents.Count(e => DetermineAiSystem(e.EventType) == s.name),
                        blockedEvents = recentEvents.Count(e => DetermineAiSystem(e.EventType) == s.name && e.IsBlocked),
                        allowedEvents = recentEvents.Count(e => DetermineAiSystem(e.EventType) == s.name && !e.IsBlocked)
                    },
                    lastBiasTest = biasTests.FirstOrDefault(b => b.System == s.name)?.LastTest,
                    biasTestsPassing = biasTests
                        .FirstOrDefault(b => b.System == s.name)?.LatestResults
                        .Count(r => r.PassesFairnessThreshold) ?? 0
                }),
                complianceStatement = "All AI systems in the IVF Information System operate under human oversight. " +
                    "No AI system makes clinical treatment decisions. All automated security decisions can be " +
                    "reviewed and overridden by authorized administrators. Bias testing is conducted per NIST AI RMF " +
                    "MEASURE 3 and ISO 42001 Annex A.8.",
                regulatoryFrameworks = new[] { "NIST AI RMF", "ISO 42001:2023", "GDPR Art. 22", "HIPAA" }
            };

            return Results.Ok(report);
        }).WithName("AiTransparencyReport");
    }

    // ─── Helper Methods ─────────────────────────────────────────────────

    private static string GetEventTypeExplanation(string eventType) => eventType switch
    {
        "AUTH_LOGIN_FAILED" => "Login attempt failed due to incorrect credentials.",
        "THREAT_IMPOSSIBLE_TRAVEL" => "Login detected from a geographic location that is impossible to reach from the previous login location within the time elapsed.",
        "THREAT_TOR_EXIT" => "Connection originated from a known Tor exit node, which is commonly associated with anonymization attempts.",
        "THREAT_VPN_DETECTED" => "Connection originated from a known VPN or proxy service.",
        "THREAT_BRUTE_FORCE" => "Multiple failed login attempts detected in rapid succession, suggesting a brute force attack.",
        "THREAT_ANOMALOUS_ACCESS" => "Access pattern deviates significantly from the user's established behavioral baseline.",
        "THREAT_INPUT_INJECTION" => "Request contained patterns consistent with injection attacks (SQL, XSS, etc.).",
        "THREAT_OFF_HOURS" => "Access attempt occurred outside the user's normal operating hours.",
        "BEHAVIOR_ANOMALY" => "User behavior significantly deviates from their 30-day baseline (z-score analysis).",
        "DATA_SENSITIVE_ACCESS" => "Access to sensitive/PHI data was logged for audit purposes.",
        "SESSION_REVOKED" => "User session was revoked due to a security policy violation.",
        "ACCOUNT_LOCKED" => "Account was locked due to exceeding the maximum number of failed login attempts.",
        "AUTH_MFA_REQUIRED" => "Multi-factor authentication was required due to elevated risk signals.",
        _ => $"Security event of type '{eventType}' was processed by the automated security system."
    };

    private static string[] GetThreatFactors(SecurityEvent securityEvent)
    {
        var factors = new List<string>();

        if (securityEvent.RiskScore > 0) factors.Add($"Risk score: {securityEvent.RiskScore}/100");
        if (!string.IsNullOrEmpty(securityEvent.IpAddress)) factors.Add($"Source IP analyzed");
        if (!string.IsNullOrEmpty(securityEvent.Country)) factors.Add($"Geographic location: {securityEvent.Country}");
        if (securityEvent.EventType.StartsWith("THREAT_")) factors.Add("Threat signal detected");
        if (securityEvent.EventType.Contains("BRUTE_FORCE")) factors.Add("Rate of failed attempts exceeded threshold");
        if (securityEvent.EventType.Contains("IMPOSSIBLE_TRAVEL")) factors.Add("Travel velocity exceeds physical possibility");
        if (securityEvent.EventType.Contains("ANOMAL")) factors.Add("Behavior deviates from 30-day baseline");
        if (securityEvent.IsBlocked) factors.Add("Automated block applied per security policy");

        if (factors.Count == 0) factors.Add("Standard security monitoring — no elevated risk factors");

        return factors.ToArray();
    }

    private static string DetermineAiSystem(string eventType)
    {
        if (eventType.StartsWith("THREAT_")) return AiSystemNames.ThreatDetection;
        if (eventType.StartsWith("BEHAVIOR_")) return AiSystemNames.BehavioralAnalytics;
        if (eventType.Contains("BIOMETRIC")) return AiSystemNames.BiometricMatcher;
        if (eventType.Contains("BOT")) return AiSystemNames.BotDetection;
        if (eventType.Contains("MFA") || eventType.Contains("STEP_UP")) return AiSystemNames.ContextualAuth;
        return AiSystemNames.ThreatDetection; // Default
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────

public record CreateBiasTestRequest(
    string AiSystemName,
    string TestType,
    string ProtectedAttribute,
    string ProtectedGroupValue,
    int SampleSize,
    int TruePositives,
    int FalsePositives,
    int TrueNegatives,
    int FalseNegatives,
    decimal BaselineFpr,
    decimal BaselineFnr,
    string TestPeriodStart,
    string TestPeriodEnd,
    string? TestRunBy = null,
    decimal FairnessThreshold = 0.25m,
    string? Explanation = null,
    string? FeatureImportance = null);

public record SetExplanationRequest(
    string Explanation,
    string? FeatureImportance = null);

public record SetRemediationRequest(string Remediation);
