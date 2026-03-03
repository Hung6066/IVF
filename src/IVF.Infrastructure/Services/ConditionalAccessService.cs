using System.Net;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Conditional Access Policy evaluator — inspired by Microsoft Entra Conditional Access.
/// Evaluates all active policies against the request context and returns the most restrictive action.
/// Policies are evaluated in priority order (lowest number = highest priority).
/// </summary>
public sealed class ConditionalAccessService(
    IvfDbContext context,
    ILogger<ConditionalAccessService> logger) : IConditionalAccessService
{
    public async Task<ConditionalAccessResult> EvaluateAsync(
        RequestSecurityContext requestContext,
        string? userRole,
        DeviceTrustResult? deviceTrust,
        CancellationToken ct = default)
    {
        var policies = await context.Set<ConditionalAccessPolicy>()
            .Where(p => p.IsEnabled && !p.IsDeleted)
            .OrderBy(p => p.Priority)
            .ToListAsync(ct);

        if (policies.Count == 0)
            return new ConditionalAccessResult(true, "Allow", null, null, null);

        foreach (var policy in policies)
        {
            if (!MatchesPolicy(policy, requestContext, userRole, deviceTrust))
                continue;

            // Skip RequireMfa/RequireStepUp if user already completed that auth level
            if (policy.Action is "RequireMfa" or "RequireStepUp" &&
                requestContext.CurrentAuthLevel >= AuthLevel.MFA)
                continue;

            // Policy conditions matched — apply the policy action
            logger.LogInformation(
                "Conditional access policy '{PolicyName}' (action={Action}) matched for user {UserId}",
                policy.Name, policy.Action, requestContext.UserId);

            return new ConditionalAccessResult(
                IsAllowed: policy.Action == "Allow",
                Action: policy.Action,
                PolicyName: policy.Name,
                Message: policy.CustomMessage ?? GetDefaultMessage(policy.Action),
                PolicyId: policy.Id);
        }

        // No policy matched — default allow
        return new ConditionalAccessResult(true, "Allow", null, null, null);
    }

    private static bool MatchesPolicy(
        ConditionalAccessPolicy policy,
        RequestSecurityContext context,
        string? userRole,
        DeviceTrustResult? deviceTrust)
    {
        // Phase 1: Exclusionary targeting — if policy targets specific roles/users
        // and the current request doesn't match, skip this policy entirely.
        if (!string.IsNullOrEmpty(policy.TargetRoles))
        {
            var roles = DeserializeArray(policy.TargetRoles);
            if (roles.Count > 0 && (string.IsNullOrEmpty(userRole) ||
                !roles.Contains(userRole, StringComparer.OrdinalIgnoreCase)))
                return false;
        }

        if (!string.IsNullOrEmpty(policy.TargetUsers))
        {
            var users = DeserializeArray(policy.TargetUsers);
            if (users.Count > 0 && (!context.UserId.HasValue ||
                !users.Contains(context.UserId.Value.ToString())))
                return false;
        }

        // Phase 2: Violation-based conditions.
        // Track whether any conditions are configured and whether any are triggered.
        // RequireMfa/RequireCompliantDevice are enforcement attributes, NOT conditions.
        bool hasConditions = false;
        bool anyConditionTriggered = false;

        // Blocked countries
        if (!string.IsNullOrEmpty(policy.BlockedCountries))
        {
            hasConditions = true;
            if (!string.IsNullOrEmpty(context.Country))
            {
                var blocked = DeserializeArray(policy.BlockedCountries);
                if (blocked.Contains(context.Country, StringComparer.OrdinalIgnoreCase))
                    anyConditionTriggered = true;
            }
        }

        // Allowed countries — trigger if country is known and not in allowed list
        if (!string.IsNullOrEmpty(policy.AllowedCountries))
        {
            hasConditions = true;
            if (!string.IsNullOrEmpty(context.Country))
            {
                var allowed = DeserializeArray(policy.AllowedCountries);
                if (allowed.Count > 0 && !allowed.Contains(context.Country, StringComparer.OrdinalIgnoreCase))
                    anyConditionTriggered = true;
            }
        }

        // Allowed IP ranges
        if (!string.IsNullOrEmpty(policy.AllowedIpRanges))
        {
            hasConditions = true;
            var ranges = DeserializeArray(policy.AllowedIpRanges);
            if (ranges.Count > 0 && !IsIpInRanges(context.IpAddress, ranges))
                anyConditionTriggered = true;
        }

        // Time windows
        if (!string.IsNullOrEmpty(policy.AllowedTimeWindows))
        {
            hasConditions = true;
            if (!IsWithinTimeWindow(policy.AllowedTimeWindows))
                anyConditionTriggered = true;
        }

        // VPN/Tor blocking
        if (policy.BlockVpnTor)
        {
            hasConditions = true;
            if (context.AdditionalSignals?.ContainsKey("is_vpn_tor") == true)
                anyConditionTriggered = true;
        }

        // Max risk level
        if (!string.IsNullOrEmpty(policy.MaxRiskLevel))
        {
            hasConditions = true;
            if (context.AdditionalSignals?.TryGetValue("risk_score", out var riskStr) == true &&
                int.TryParse(riskStr, out var riskScore) &&
                int.TryParse(policy.MaxRiskLevel, out var maxRisk) &&
                riskScore > maxRisk)
            {
                anyConditionTriggered = true;
            }
        }

        // Device trust requirement
        if (!string.IsNullOrEmpty(policy.RequiredDeviceTrust))
        {
            hasConditions = true;
            if (deviceTrust is not null)
            {
                var requiredLevel = Enum.TryParse<DeviceTrustLevel>(policy.RequiredDeviceTrust, true, out var level)
                    ? level : DeviceTrustLevel.Trusted;
                if (deviceTrust.TrustLevel < requiredLevel)
                    anyConditionTriggered = true;
            }
        }

        // If no violation conditions configured, match on targeting alone
        // (e.g., "MFA for Admin Roles" targets Admins with no extra conditions)
        if (!hasConditions)
            return true;

        // Match only if at least one condition was triggered
        return anyConditionTriggered;
    }

    private static List<string> DeserializeArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static bool IsIpInRanges(string ipAddress, List<string> ranges)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        foreach (var range in ranges)
        {
            var parts = range.Split('/');
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var network) || !int.TryParse(parts[1], out var bits))
                continue;

            var ipBytes = ip.GetAddressBytes();
            var networkBytes = network.GetAddressBytes();
            if (ipBytes.Length != networkBytes.Length)
                continue;

            var totalBits = ipBytes.Length * 8;
            var matchBits = Math.Min(bits, totalBits);

            var match = true;
            for (var i = 0; i < matchBits; i++)
            {
                var byteIndex = i / 8;
                var bitIndex = 7 - (i % 8);
                if (((ipBytes[byteIndex] >> bitIndex) & 1) != ((networkBytes[byteIndex] >> bitIndex) & 1))
                {
                    match = false;
                    break;
                }
            }

            if (match) return true;
        }
        return false;
    }

    private static bool IsWithinTimeWindow(string timeWindowsJson)
    {
        try
        {
            var windows = JsonSerializer.Deserialize<List<TimeWindow>>(timeWindowsJson);
            if (windows is null || windows.Count == 0) return true;

            var now = DateTime.UtcNow;
            var dayName = now.DayOfWeek.ToString()[..3]; // "Mon", "Tue", ...

            foreach (var window in windows)
            {
                if (window.Days is not null && !window.Days.Contains(dayName, StringComparer.OrdinalIgnoreCase))
                    continue;

                if (TimeOnly.TryParse(window.Start, out var start) && TimeOnly.TryParse(window.End, out var end))
                {
                    var current = TimeOnly.FromDateTime(now);
                    if (current >= start && current <= end)
                        return true;
                }
            }
            return false;
        }
        catch
        {
            return true; // On parse failure, don't block
        }
    }

    private static string GetDefaultMessage(string action) => action switch
    {
        "Block" => "Access denied by security policy",
        "RequireMfa" => "Multi-factor authentication required by security policy",
        "RequireStepUp" => "Additional verification required by security policy",
        "RequireDeviceCompliance" => "Device does not meet compliance requirements",
        _ => "Access policy applied"
    };

    private record TimeWindow(string? Start, string? End, List<string>? Days);
}
