using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Behavioral analytics engine — builds user behavior profiles from login history
/// and detects anomalies using statistical methods (z-score, velocity checks).
/// Inspired by Amazon Cognito Advanced Security + Google Risk Analysis.
/// </summary>
public sealed class BehavioralAnalyticsService(
    IvfDbContext context,
    ILogger<BehavioralAnalyticsService> logger) : IBehavioralAnalyticsService
{
    public async Task UpdateProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var history = await context.UserLoginHistories
            .Where(h => h.UserId == userId && h.IsSuccess && !h.IsDeleted)
            .OrderByDescending(h => h.LoginAt)
            .Take(100)
            .ToListAsync(ct);

        if (history.Count == 0) return;

        var profile = await context.Set<UserBehaviorProfile>()
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, ct);

        var loginHours = history.Select(h => h.LoginAt.Hour).ToList();
        var avgHour = loginHours.Count > 0 ? (int)loginHours.Average() : 8;
        var stdDev = loginHours.Count > 1
            ? Math.Sqrt(loginHours.Average(h => Math.Pow(h - avgHour, 2)))
            : 4; // default 4-hour window

        var startHour = Math.Max(0, avgHour - (int)(2 * stdDev));
        var endHour = Math.Min(23, avgHour + (int)(2 * stdDev));

        var commonIps = history
            .Where(h => !string.IsNullOrEmpty(h.IpAddress))
            .GroupBy(h => h.IpAddress!)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();

        var commonCountries = history
            .Where(h => !string.IsNullOrEmpty(h.Country))
            .Select(h => h.Country!)
            .Distinct()
            .ToList();

        var commonFingerprints = history
            .Where(h => !string.IsNullOrEmpty(h.DeviceFingerprint))
            .GroupBy(h => h.DeviceFingerprint!)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        var commonUAs = history
            .Where(h => !string.IsNullOrEmpty(h.UserAgent))
            .GroupBy(h => NormalizeUserAgent(h.UserAgent!))
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        var avgDuration = history
            .Where(h => h.SessionDuration.HasValue)
            .Select(h => h.SessionDuration!.Value.TotalMinutes)
            .DefaultIfEmpty(30)
            .Average();

        var totalFailed = await context.UserLoginHistories
            .CountAsync(h => h.UserId == userId && !h.IsSuccess && !h.IsDeleted, ct);

        var typicalHours = JsonSerializer.Serialize(new { start = startHour, end = endHour, timezone = "UTC" });

        if (profile is null)
        {
            profile = UserBehaviorProfile.Create(userId);
            context.Set<UserBehaviorProfile>().Add(profile);
        }

        profile.UpdateProfile(
            typicalLoginHours: typicalHours,
            commonIpAddresses: JsonSerializer.Serialize(commonIps),
            commonCountries: JsonSerializer.Serialize(commonCountries),
            commonDeviceFingerprints: JsonSerializer.Serialize(commonFingerprints),
            commonUserAgents: JsonSerializer.Serialize(commonUAs),
            averageSessionDurationMinutes: (decimal)avgDuration,
            totalLogins: history.Count,
            totalFailedLogins: totalFailed,
            lastLoginAt: history.FirstOrDefault()?.LoginAt,
            lastFailedLoginAt: null);

        await context.SaveChangesAsync(ct);

        logger.LogDebug("Updated behavior profile for user {UserId}: {Logins} logins, {IPs} IPs, {Countries} countries",
            userId, history.Count, commonIps.Count, commonCountries.Count);
    }

    public async Task<BehaviorAnalysisResult> AnalyzeLoginAsync(
        Guid userId,
        string ipAddress,
        string? userAgent,
        string? country,
        DateTime loginTime,
        CancellationToken ct = default)
    {
        var anomalyFactors = new List<string>();
        var anomalyScore = 0m;

        var profile = await context.Set<UserBehaviorProfile>()
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, ct);

        if (profile is null)
        {
            // No profile yet — can't detect anomalies, just check for rapid succession
            var recentCount = await context.UserLoginHistories
                .CountAsync(h => h.UserId == userId && !h.IsDeleted &&
                    h.LoginAt > DateTime.UtcNow.AddMinutes(-5), ct);
            if (recentCount > 3)
            {
                anomalyFactors.Add("rapid_succession");
                anomalyScore += 30;
            }

            return new BehaviorAnalysisResult(anomalyScore > 20, anomalyScore, anomalyFactors);
        }

        // 1. Check login time anomaly
        if (profile.TypicalLoginHours is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(profile.TypicalLoginHours);
                var start = doc.RootElement.GetProperty("start").GetInt32();
                var end = doc.RootElement.GetProperty("end").GetInt32();
                if (loginTime.Hour < start || loginTime.Hour > end)
                {
                    anomalyFactors.Add("unusual_time");
                    anomalyScore += 15;
                }
            }
            catch { /* ignore parse errors */ }
        }

        // 2. Check IP anomaly
        if (profile.CommonIpAddresses is not null)
        {
            var knownIps = DeserializeList(profile.CommonIpAddresses);
            if (knownIps.Count > 0 && !knownIps.Contains(ipAddress))
            {
                anomalyFactors.Add("new_ip");
                anomalyScore += 15;
            }
        }

        // 3. Check country anomaly
        if (!string.IsNullOrEmpty(country) && profile.CommonCountries is not null)
        {
            var knownCountries = DeserializeList(profile.CommonCountries);
            if (knownCountries.Count > 0 && !knownCountries.Contains(country, StringComparer.OrdinalIgnoreCase))
            {
                anomalyFactors.Add("new_country");
                anomalyScore += 25;
            }
        }

        // 4. Check device anomaly
        if (!string.IsNullOrEmpty(userAgent) && profile.CommonUserAgents is not null)
        {
            var knownUAs = DeserializeList(profile.CommonUserAgents);
            var normalizedUA = NormalizeUserAgent(userAgent);
            if (knownUAs.Count > 0 && !knownUAs.Contains(normalizedUA))
            {
                anomalyFactors.Add("new_device");
                anomalyScore += 20;
            }
        }

        // 5. Rapid succession check
        var recentLogins = await context.UserLoginHistories
            .CountAsync(h => h.UserId == userId && !h.IsDeleted &&
                h.LoginAt > DateTime.UtcNow.AddMinutes(-5), ct);
        if (recentLogins > 3)
        {
            anomalyFactors.Add("rapid_succession");
            anomalyScore += 25;
        }

        // Cap at 100
        anomalyScore = Math.Min(anomalyScore, 100);

        return new BehaviorAnalysisResult(
            IsAnomalous: anomalyScore >= 25,
            AnomalyScore: anomalyScore,
            AnomalyFactors: anomalyFactors);
    }

    private static List<string> DeserializeList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private static string NormalizeUserAgent(string ua)
    {
        // Extract browser family + OS family (not version-specific)
        if (ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Edg", StringComparison.OrdinalIgnoreCase))
            return ua.Contains("Windows") ? "Chrome/Windows" :
                   ua.Contains("Mac") ? "Chrome/Mac" :
                   ua.Contains("Linux") ? "Chrome/Linux" : "Chrome/Other";
        if (ua.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            return ua.Contains("Windows") ? "Firefox/Windows" :
                   ua.Contains("Mac") ? "Firefox/Mac" : "Firefox/Linux";
        if (ua.Contains("Safari", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Chrome"))
            return "Safari/Mac";
        if (ua.Contains("Edg", StringComparison.OrdinalIgnoreCase))
            return ua.Contains("Windows") ? "Edge/Windows" : "Edge/Other";
        return "Unknown";
    }
}
