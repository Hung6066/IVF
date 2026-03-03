using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Contextual authentication — evaluates login context against the user's
/// behavior profile to determine if additional verification is needed.
/// Inspired by Google's Contextual Access + AWS IAM Identity Center risk assessment.
/// </summary>
public sealed class ContextualAuthService(
    IvfDbContext context,
    ILogger<ContextualAuthService> logger) : IContextualAuthService
{
    private const int LongAbsenceDays = 30;

    public async Task<ContextualAuthResult> EvaluateContextAsync(
        Guid userId,
        string ipAddress,
        string? userAgent,
        string? deviceFingerprint,
        string? country,
        CancellationToken ct = default)
    {
        var triggers = new List<string>();

        // Load user behavior profile
        var profile = await context.Set<UserBehaviorProfile>()
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, ct);

        // Load recent login history
        var recentLogins = await context.UserLoginHistories
            .Where(h => h.UserId == userId && h.IsSuccess && !h.IsDeleted)
            .OrderByDescending(h => h.LoginAt)
            .Take(20)
            .ToListAsync(ct);

        // 1. New device detection
        if (!string.IsNullOrEmpty(deviceFingerprint))
        {
            var knownDevice = await context.DeviceRisks
                .AnyAsync(d => d.UserId == userId.ToString() && d.DeviceId == deviceFingerprint && !d.IsDeleted, ct);
            if (!knownDevice)
                triggers.Add("new_device");
        }

        // 2. New country detection
        if (!string.IsNullOrEmpty(country) && recentLogins.Count > 0)
        {
            var knownCountries = recentLogins
                .Where(h => !string.IsNullOrEmpty(h.Country))
                .Select(h => h.Country!)
                .Distinct()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (knownCountries.Count > 0 && !knownCountries.Contains(country))
                triggers.Add("new_country");
        }

        // 3. Unusual time detection
        if (profile?.TypicalLoginHours is not null)
        {
            try
            {
                var hours = JsonSerializer.Deserialize<TypicalHours>(profile.TypicalLoginHours);
                if (hours is not null)
                {
                    var currentHour = DateTime.UtcNow.Hour;
                    if (currentHour < hours.Start || currentHour > hours.End)
                        triggers.Add("unusual_time");
                }
            }
            catch
            {
                // Profile data corrupt — skip time check
            }
        }
        else if (recentLogins.Count >= 5)
        {
            // Infer typical hours from login history
            var loginHours = recentLogins.Select(h => h.LoginAt.Hour).ToList();
            var avgHour = loginHours.Average();
            var currentHour = DateTime.UtcNow.Hour;
            var stdDev = Math.Sqrt(loginHours.Average(h => Math.Pow(h - avgHour, 2)));

            // If current login is > 2 standard deviations from mean, flag
            if (stdDev > 0 && Math.Abs(currentHour - avgHour) > 2 * stdDev)
                triggers.Add("unusual_time");
        }

        // 4. Long absence detection
        var lastLogin = recentLogins.FirstOrDefault()?.LoginAt;
        if (lastLogin.HasValue && (DateTime.UtcNow - lastLogin.Value).TotalDays > LongAbsenceDays)
            triggers.Add("long_absence");

        // 5. New IP detection (from different subnet)
        if (recentLogins.Count >= 3)
        {
            var knownIpPrefixes = recentLogins
                .Where(h => !string.IsNullOrEmpty(h.IpAddress))
                .Select(h => GetIpPrefix(h.IpAddress!))
                .Where(p => p is not null)
                .Distinct()
                .ToHashSet();

            var currentPrefix = GetIpPrefix(ipAddress);
            if (currentPrefix is not null && knownIpPrefixes.Count > 0 && !knownIpPrefixes.Contains(currentPrefix))
                triggers.Add("new_ip_range");
        }

        // Determine recommended action based on trigger severity
        string? recommendedAction = null;
        if (triggers.Count >= 3)
            recommendedAction = "mfa";
        else if (triggers.Contains("new_device") && triggers.Contains("new_country"))
            recommendedAction = "mfa";
        else if (triggers.Contains("long_absence"))
            recommendedAction = "mfa";
        else if (triggers.Count > 0)
            recommendedAction = "mfa";

        if (triggers.Count > 0)
        {
            logger.LogInformation("Contextual auth triggers for user {UserId}: {Triggers}",
                userId, string.Join(", ", triggers));
        }

        return new ContextualAuthResult(
            RequiresAdditionalVerification: triggers.Count > 0,
            Triggers: triggers,
            RecommendedAction: recommendedAction);
    }

    private static string? GetIpPrefix(string ip)
    {
        var parts = ip.Split('.');
        return parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : null;
    }

    private record TypicalHours(int Start, int End, string? Timezone);
}
