using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

public class ZeroTrustService : IZeroTrustService
{
    private readonly IvfDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ZeroTrustService> _logger;
    private readonly TimeSpan _policyCacheExpiration = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _freshSessionThreshold = TimeSpan.FromMinutes(15);

    private const string PoliciesCacheKey = "zt:policies";

    public ZeroTrustService(
        IvfDbContext context,
        IMemoryCache cache,
        ILogger<ZeroTrustService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ZTAccessDecision> CheckVaultAccessAsync(CheckZTAccessRequest request, CancellationToken ct = default)
    {
        var actionName = request.Action.ToString();
        var policies = await GetCachedPoliciesAsync(ct);
        var policy = policies.FirstOrDefault(p => p.Action == actionName);

        if (policy is null || !policy.IsActive)
        {
            return CreateDenied(request.Action, "No active policy found for action", ["PolicyNotFound"]);
        }

        var failedChecks = new List<string>();
        var ctx = request.Context;

        // 1. Auth level check
        if (!CheckAuthLevel(ctx.CurrentAuthLevel, policy.RequiredAuthLevel))
        {
            failedChecks.Add($"Insufficient auth level: required={policy.RequiredAuthLevel}, current={ctx.CurrentAuthLevel}");
        }

        // 2. Device risk calculation
        var (riskLevel, riskScore, factors) = CalculateDeviceRisk(ctx);

        var maxAllowed = ParseRiskLevel(policy.MaxAllowedRisk);
        if (riskLevel > maxAllowed)
        {
            failedChecks.Add($"Device risk too high: level={riskLevel}, maxAllowed={maxAllowed}, score={riskScore}");
        }

        // 3. Trusted device check
        if (policy.RequireTrustedDevice)
        {
            var device = await _context.DeviceRisks
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == ctx.UserId && d.DeviceId == ctx.DeviceId, ct);

            if (device is null || !device.IsTrusted)
            {
                failedChecks.Add("Trusted device required but device is not trusted");
            }
        }

        // 4. Fresh session check (15-min password age)
        if (policy.RequireFreshSession)
        {
            if (ctx.LastPasswordVerification is null ||
                DateTime.UtcNow - ctx.LastPasswordVerification.Value > _freshSessionThreshold)
            {
                failedChecks.Add($"Fresh session required: last verification was {ctx.LastPasswordVerification?.ToString("o") ?? "never"}");
            }
        }

        // 5. Geo-fence check
        if (policy.RequireGeoFence && !string.IsNullOrEmpty(policy.AllowedCountries))
        {
            var allowed = policy.AllowedCountries.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (string.IsNullOrEmpty(ctx.Country) || !allowed.Contains(ctx.Country, StringComparer.OrdinalIgnoreCase))
            {
                failedChecks.Add($"Geo-fence violation: country={ctx.Country ?? "unknown"}, allowed={policy.AllowedCountries}");
            }
        }

        // 6. VPN/Tor blocking
        if (policy.BlockVpnTor && (ctx.IsVpn || ctx.IsTor))
        {
            failedChecks.Add($"VPN/Tor blocked: IsVpn={ctx.IsVpn}, IsTor={ctx.IsTor}");
        }

        // 7. Anomaly detection
        if (policy.BlockAnomaly && ctx.HasActiveAnomaly)
        {
            failedChecks.Add("Active anomaly detected");
        }

        // 8. Break-glass override
        var breakGlassUsed = false;
        if (failedChecks.Count > 0 && request.UseBreakGlassOverride && policy.AllowBreakGlassOverride)
        {
            _logger.LogWarning(
                "Break-glass override used for action {Action} by user {UserId}. RequestId: {RequestId}. Failed checks: {FailedChecks}",
                actionName, ctx.UserId, request.BreakGlassRequestId, string.Join("; ", failedChecks));

            breakGlassUsed = true;
            failedChecks.Clear();
        }

        // Save device risk to DB
        await SaveDeviceRiskAsync(ctx, riskLevel, riskScore, factors, ct);

        var allowed2 = failedChecks.Count == 0;
        var requiresStepUp = !allowed2 && failedChecks.Any(c => c.StartsWith("Insufficient auth level"));
        var requiredAuth = !allowed2 ? ParseAuthLevel(policy.RequiredAuthLevel) : null;

        var decision = new ZTAccessDecision(
            Allowed: allowed2,
            Action: request.Action,
            Reason: allowed2 ? "Access granted" : $"Access denied: {failedChecks.Count} check(s) failed",
            FailedChecks: failedChecks,
            DeviceRiskLevel: riskLevel,
            DeviceRiskScore: riskScore,
            RequiresStepUp: requiresStepUp,
            RequiredAuthLevel: requiredAuth,
            BreakGlassOverrideUsed: breakGlassUsed,
            DecisionTime: DateTime.UtcNow
        );

        // Log audit
        _logger.LogInformation(
            "ZT Decision: Action={Action}, User={UserId}, Allowed={Allowed}, Risk={RiskLevel}/{RiskScore}, BreakGlass={BreakGlass}",
            actionName, ctx.UserId, decision.Allowed, riskLevel, riskScore, breakGlassUsed);

        return decision;
    }

    public async Task<List<ZTPolicy>> GetAllPoliciesAsync(CancellationToken ct = default)
    {
        return await _context.ZTPolicies
            .AsNoTracking()
            .OrderBy(p => p.Action)
            .ToListAsync(ct);
    }

    public async Task<bool> UpdatePolicyAsync(ZTVaultAction action, ZTPolicy policy, CancellationToken ct = default)
    {
        var existing = await _context.ZTPolicies
            .FirstOrDefaultAsync(p => p.Action == action.ToString(), ct);

        if (existing is null)
            return false;

        existing.Update(
            policy.RequiredAuthLevel,
            policy.MaxAllowedRisk,
            policy.RequireTrustedDevice,
            policy.RequireFreshSession,
            policy.BlockAnomaly,
            policy.RequireGeoFence,
            policy.AllowedCountries,
            policy.BlockVpnTor,
            policy.AllowBreakGlassOverride,
            policy.UpdatedBy);

        await _context.SaveChangesAsync(ct);

        // Invalidate cache
        _cache.Remove(PoliciesCacheKey);

        return true;
    }

    public Task RefreshPoliciesAsync(CancellationToken ct = default)
    {
        _cache.Remove(PoliciesCacheKey);
        _logger.LogInformation("Zero Trust policy cache refreshed");
        return Task.CompletedTask;
    }

    // ─── Private Helpers ───

    private async Task<List<ZTPolicy>> GetCachedPoliciesAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(PoliciesCacheKey, out List<ZTPolicy>? cached) && cached is not null)
            return cached;

        var policies = await _context.ZTPolicies
            .AsNoTracking()
            .Where(p => p.IsActive)
            .ToListAsync(ct);

        _cache.Set(PoliciesCacheKey, policies, _policyCacheExpiration);
        return policies;
    }

    private static (RiskLevel Level, decimal Score, string Factors) CalculateDeviceRisk(ZTAccessContext ctx)
    {
        var score = 0m;
        var factors = new List<string>();

        if (ctx.IsVpn || ctx.IsTor)
        {
            score += 30;
            factors.Add(ctx.IsVpn ? "VPN detected" : "Tor detected");
        }

        if (string.IsNullOrEmpty(ctx.Country))
        {
            score += 20;
            factors.Add("Unknown country (possible geo change)");
        }

        if (ctx.HasActiveAnomaly)
        {
            score += 40;
            factors.Add("Active anomaly detected");
        }

        // New device check — device without prior trust is considered new
        // (This is a baseline; the caller can enrich context with more signals)
        if (string.IsNullOrEmpty(ctx.DeviceId))
        {
            score += 25;
            factors.Add("New/unknown device");
        }

        var level = score switch
        {
            >= 70 => RiskLevel.Critical,
            >= 50 => RiskLevel.High,
            >= 30 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        return (level, score, string.Join("; ", factors));
    }

    private async Task SaveDeviceRiskAsync(ZTAccessContext ctx, RiskLevel riskLevel, decimal riskScore, string factors, CancellationToken ct)
    {
        try
        {
            var existing = await _context.DeviceRisks
                .FirstOrDefaultAsync(d => d.UserId == ctx.UserId && d.DeviceId == ctx.DeviceId, ct);

            if (existing is not null)
            {
                existing.UpdateRisk(riskLevel, riskScore, factors);
            }
            else
            {
                var deviceRisk = DeviceRisk.Create(
                    userId: ctx.UserId,
                    deviceId: ctx.DeviceId,
                    riskLevel: riskLevel,
                    riskScore: riskScore,
                    factors: factors,
                    isTrusted: false,
                    ipAddress: ctx.IpAddress,
                    country: ctx.Country);

                await _context.DeviceRisks.AddAsync(deviceRisk, ct);
            }

            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save device risk for user {UserId}, device {DeviceId}", ctx.UserId, ctx.DeviceId);
            // Don't fail the access decision for a persistence error
        }
    }

    private static bool CheckAuthLevel(AuthLevel current, string? required)
    {
        if (string.IsNullOrEmpty(required))
            return true;

        var requiredLevel = ParseAuthLevel(required);
        return requiredLevel is null || current >= requiredLevel.Value;
    }

    private static AuthLevel? ParseAuthLevel(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return Enum.TryParse<AuthLevel>(value, ignoreCase: true, out var level) ? level : null;
    }

    private static RiskLevel ParseRiskLevel(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return RiskLevel.Critical; // Default to most permissive when not specified

        return Enum.TryParse<RiskLevel>(value, ignoreCase: true, out var level) ? level : RiskLevel.Critical;
    }

    private static ZTAccessDecision CreateDenied(ZTVaultAction action, string reason, List<string> failedChecks)
    {
        return new ZTAccessDecision(
            Allowed: false,
            Action: action,
            Reason: reason,
            FailedChecks: failedChecks,
            DeviceRiskLevel: null,
            DeviceRiskScore: null,
            RequiresStepUp: false,
            RequiredAuthLevel: null,
            BreakGlassOverrideUsed: false,
            DecisionTime: DateTime.UtcNow
        );
    }
}
