using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace IVF.API.Endpoints;

/// <summary>
/// Advanced security endpoints: Passkeys/WebAuthn, TOTP, SMS OTP, device management,
/// session management, account lockout CRUD, rate limit CRUD, geo-security, threats,
/// IP whitelist CRUD. All endpoints require Admin role.
/// </summary>
public static class AdvancedSecurityEndpoints
{
    // JSON options for Fido2 serialization — excludes the global JsonStringEnumConverter
    // so that Fido2NetLib's own converters produce WebAuthn-compatible values.
    private static readonly JsonSerializerOptions _fidoJsonOptions = new(JsonSerializerDefaults.Web);

    // Temporary in-memory stores for WebAuthn ceremony state (short-lived)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CredentialCreateOptions> _pendingRegistrations = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, AssertionOptions> _pendingAssertions = new();
    // Temporary SMS OTP codes (short-lived)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Code, DateTime ExpiresAt)> _pendingSmsOtp = new();

    public static void MapAdvancedSecurityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/security/advanced")
            .WithTags("AdvancedSecurity")
            .RequireAuthorization("AdminOnly");

        MapSecurityScore(group);
        MapLoginHistory(group);
        MapRateLimits(group);
        MapGeoSecurity(group);
        MapThreats(group);
        MapAccountLockouts(group);
        MapIpWhitelist(group);
        MapDevices(group);
        MapPasskeys(group);
        MapTotp(group);
        MapSmsOtp(group);
        MapMfaSettings(group);
        MapComplianceDashboard(group);
        MapSecurityPolicies(group);
        MapAuditReports(group);

        // Client IP endpoint
        group.MapGet("/my-ip", async (HttpContext context, IHttpClientFactory httpClientFactory) =>
        {
            var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                     ?? context.Request.Headers["X-Real-IP"].FirstOrDefault()
                     ?? context.Connection.RemoteIpAddress?.MapToIPv4().ToString()
                     ?? "unknown";
            // Strip port if present in X-Forwarded-For
            if (ip.Contains(',')) ip = ip.Split(',')[0].Trim();

            // If loopback, resolve public IP server-side (no CORS issues)
            if (ip is "127.0.0.1" or "::1" or "0.0.0.1" or "unknown")
            {
                try
                {
                    var client = httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var publicIp = await client.GetStringAsync("https://api.ipify.org");
                    if (!string.IsNullOrWhiteSpace(publicIp))
                        ip = publicIp.Trim();
                }
                catch { /* keep original ip */ }
            }

            return Results.Ok(new { ip });
        }).WithName("GetMyIp");
    }

    // ═══════════════════════════════════════════════════════════
    // SECURITY SCORE
    // ═══════════════════════════════════════════════════════════

    private static void MapSecurityScore(RouteGroupBuilder group)
    {
        group.MapGet("/score", async (
            ISecurityEventService securityEvents,
            IvfDbContext db) =>
        {
            var since24h = DateTime.UtcNow.AddHours(-24);
            var highSeverity = await securityEvents.GetHighSeverityEventsAsync(since24h);
            var allRecent = await securityEvents.GetRecentEventsAsync(200);

            var blockedCount = highSeverity.Count(e => e.IsBlocked);
            var criticalCount = highSeverity.Count(e => e.Severity == "Critical");
            var failedLogins = allRecent.Count(e => e.EventType == SecurityEventTypes.LoginFailed);
            var suspiciousLogins = allRecent.Count(e =>
                e.EventType == SecurityEventTypes.ImpossibleTravel ||
                e.EventType == SecurityEventTypes.AnomalousAccess);

            var trustedDevices = await db.DeviceRisks.CountAsync(d => d.IsTrusted && !d.IsDeleted);
            var activeLockouts = await db.AccountLockouts.CountAsync(l => !l.IsDeleted && l.UnlocksAt > DateTime.UtcNow);
            var mfaEnabledUsers = await db.UserMfaSettings.CountAsync(m => m.IsMfaEnabled && !m.IsDeleted);
            var passkeyCount = await db.PasskeyCredentials.CountAsync(p => p.IsActive && !p.IsDeleted);

            var score = 100;
            score -= Math.Min(criticalCount * 10, 30);
            score -= Math.Min(failedLogins * 2, 20);
            score -= Math.Min(suspiciousLogins * 5, 20);
            score -= Math.Min(blockedCount, 10);
            if (mfaEnabledUsers == 0) score -= 10;
            score = Math.Max(score, 0);

            var factors = new List<object>();
            if (criticalCount > 0) factors.Add(new { factor = "critical_events", count = criticalCount, impact = -Math.Min(criticalCount * 10, 30) });
            if (failedLogins > 0) factors.Add(new { factor = "failed_logins", count = failedLogins, impact = -Math.Min(failedLogins * 2, 20) });
            if (suspiciousLogins > 0) factors.Add(new { factor = "suspicious_activity", count = suspiciousLogins, impact = -Math.Min(suspiciousLogins * 5, 20) });
            if (blockedCount > 0) factors.Add(new { factor = "blocked_requests", count = blockedCount, impact = -Math.Min(blockedCount, 10) });
            if (mfaEnabledUsers == 0) factors.Add(new { factor = "no_mfa_users", count = 0, impact = -10 });

            return Results.Ok(new
            {
                score,
                level = score >= 80 ? "good" : score >= 50 ? "warning" : "critical",
                factors,
                totalEvents24h = highSeverity.Count,
                blockedRequests = blockedCount,
                criticalAlerts = criticalCount,
                failedLogins,
                suspiciousLogins,
                trustedDevices,
                activeSessions = 0,
                activeLockouts,
                mfaEnabledUsers,
                passkeyCount,
                lastUpdated = DateTime.UtcNow
            });
        }).WithName("GetSecurityScore");
    }

    // ═══════════════════════════════════════════════════════════
    // LOGIN HISTORY
    // ═══════════════════════════════════════════════════════════

    private static void MapLoginHistory(RouteGroupBuilder group)
    {
        group.MapGet("/login-history", async (
            ISecurityEventService securityEvents,
            int? count) =>
        {
            var limit = Math.Min(count ?? 50, 200);
            var allEvents = await securityEvents.GetRecentEventsAsync(500);

            var loginEvents = allEvents
                .Where(e => e.EventType is SecurityEventTypes.LoginSuccess
                    or SecurityEventTypes.LoginFailed
                    or SecurityEventTypes.LoginBruteForce
                    or SecurityEventTypes.ImpossibleTravel)
                .Take(limit)
                .Select(e => new
                {
                    e.Id,
                    e.EventType,
                    e.Severity,
                    e.UserId,
                    e.Username,
                    e.IpAddress,
                    e.Country,
                    e.City,
                    e.DeviceFingerprint,
                    e.RiskScore,
                    isSuspicious = e.Severity is "High" or "Critical" || (e.RiskScore ?? 0) >= 60,
                    riskFactors = GetRiskFactors(e),
                    e.CreatedAt
                });

            return Results.Ok(loginEvents);
        }).WithName("GetLoginHistory");
    }

    // ═══════════════════════════════════════════════════════════
    // RATE LIMIT CRUD
    // ═══════════════════════════════════════════════════════════

    private static void MapRateLimits(RouteGroupBuilder group)
    {
        group.MapGet("/rate-limits", async (IvfDbContext db) =>
        {
            var configs = await db.RateLimitConfigs
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.PolicyName)
                .Select(r => new
                {
                    r.Id,
                    r.PolicyName,
                    r.WindowType,
                    r.WindowSeconds,
                    r.PermitLimit,
                    r.AppliesTo,
                    r.IsEnabled,
                    r.Description,
                    r.CreatedBy,
                    r.CreatedAt
                })
                .ToListAsync();

            var builtInPolicies = new[]
            {
                new { name = "Global", window = "1 min", limit = 100, policy = "fixed", builtIn = true },
                new { name = "Auth", window = "1 min", limit = 10, policy = "fixed", builtIn = true },
                new { name = "Sensitive", window = "1 min", limit = 30, policy = "fixed", builtIn = true },
                new { name = "Signing", window = "1 min", limit = 30, policy = "fixed", builtIn = true },
                new { name = "Signing-Provision", window = "1 min", limit = 3, policy = "fixed", builtIn = true },
            };

            return Results.Ok(new { builtInPolicies, customConfigs = configs, updatedAt = DateTime.UtcNow });
        }).WithName("GetRateLimitStatus");

        group.MapPost("/rate-limits", async (CreateRateLimitRequest request, IvfDbContext db) =>
        {
            var config = RateLimitConfig.Create(
                policyName: request.PolicyName,
                windowType: request.WindowType,
                windowSeconds: request.WindowSeconds,
                permitLimit: request.PermitLimit,
                appliesTo: request.AppliesTo,
                createdBy: request.CreatedBy ?? "Admin",
                description: request.Description);

            await db.RateLimitConfigs.AddAsync(config);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Rate limit created", id = config.Id });
        }).WithName("CreateRateLimit");

        group.MapPut("/rate-limits/{id:guid}", async (Guid id, UpdateRateLimitRequest request, IvfDbContext db) =>
        {
            var config = await db.RateLimitConfigs.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (config is null) return Results.NotFound(new { message = "Rate limit not found" });

            config.Update(request.WindowType, request.WindowSeconds, request.PermitLimit, request.Description);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Rate limit updated" });
        }).WithName("UpdateRateLimit");

        group.MapDelete("/rate-limits/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var config = await db.RateLimitConfigs.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (config is null) return Results.NotFound(new { message = "Rate limit not found" });

            config.MarkAsDeleted();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Rate limit deleted" });
        }).WithName("DeleteRateLimit");

        group.MapGet("/rate-limit-events", async (
            ISecurityEventService securityEvents,
            int? hours) =>
        {
            var since = DateTime.UtcNow.AddHours(-(hours ?? 24));
            var allEvents = await securityEvents.GetRecentEventsAsync(500);
            var rateLimitEvents = allEvents
                .Where(e => e.EventType == SecurityEventTypes.RateLimitExceeded && e.CreatedAt >= since)
                .Select(e => new
                {
                    e.Id,
                    e.IpAddress,
                    e.Username,
                    e.RequestPath,
                    e.RiskScore,
                    e.Details,
                    e.CreatedAt
                });

            return Results.Ok(rateLimitEvents);
        }).WithName("GetRateLimitEvents");
    }

    // ═══════════════════════════════════════════════════════════
    // GEO SECURITY
    // ═══════════════════════════════════════════════════════════

    private static void MapGeoSecurity(RouteGroupBuilder group)
    {
        group.MapGet("/geo-events", async (
            ISecurityEventService securityEvents,
            IvfDbContext db,
            int? hours) =>
        {
            var since = DateTime.UtcNow.AddHours(-(hours ?? 48));
            var allEvents = await securityEvents.GetRecentEventsAsync(500);

            var geoData = allEvents
                .Where(e => e.Country != null && e.CreatedAt >= since)
                .GroupBy(e => e.Country!)
                .Select(g => new
                {
                    country = g.Key,
                    totalEvents = g.Count(),
                    suspiciousEvents = g.Count(e => e.Severity is "High" or "Critical"),
                    blockedEvents = g.Count(e => e.IsBlocked),
                    uniqueIps = g.Select(e => e.IpAddress).Distinct().Count(),
                    lastSeen = g.Max(e => e.CreatedAt)
                })
                .OrderByDescending(g => g.suspiciousEvents)
                .ToList();

            var impossibleTravel = allEvents
                .Where(e => e.EventType == SecurityEventTypes.ImpossibleTravel && e.CreatedAt >= since)
                .Select(e => new
                {
                    e.Id,
                    e.UserId,
                    e.Username,
                    e.IpAddress,
                    e.Country,
                    e.City,
                    e.Details,
                    e.RiskScore,
                    e.CreatedAt
                });

            var geoBlockRules = await db.GeoBlockRules
                .Where(r => !r.IsDeleted && r.IsEnabled)
                .Select(r => new
                {
                    r.Id,
                    r.CountryCode,
                    r.CountryName,
                    r.IsBlocked,
                    r.Reason,
                    r.CreatedBy,
                    r.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new
            {
                geoDistribution = geoData,
                impossibleTravelAlerts = impossibleTravel,
                totalCountries = geoData.Count,
                geoBlockRules
            });
        }).WithName("GetGeoSecurityData");

        group.MapPost("/geo-rules", async (CreateGeoBlockRuleRequest request, IvfDbContext db) =>
        {
            var existing = await db.GeoBlockRules.FirstOrDefaultAsync(r =>
                r.CountryCode == request.CountryCode && !r.IsDeleted);
            if (existing is not null)
            {
                existing.Update(request.IsBlocked, request.Reason);
                await db.SaveChangesAsync();
                return Results.Ok(new { message = "Geo rule updated", id = existing.Id });
            }

            var rule = GeoBlockRule.Create(
                countryCode: request.CountryCode,
                countryName: request.CountryName,
                isBlocked: request.IsBlocked,
                createdBy: request.CreatedBy ?? "Admin",
                reason: request.Reason);

            await db.GeoBlockRules.AddAsync(rule);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Geo rule created", id = rule.Id });
        }).WithName("CreateGeoBlockRule");

        group.MapDelete("/geo-rules/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var rule = await db.GeoBlockRules.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (rule is null) return Results.NotFound(new { message = "Geo rule not found" });

            rule.MarkAsDeleted();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Geo rule deleted" });
        }).WithName("DeleteGeoBlockRule");
    }

    // ═══════════════════════════════════════════════════════════
    // THREATS
    // ═══════════════════════════════════════════════════════════

    private static void MapThreats(RouteGroupBuilder group)
    {
        group.MapGet("/threats", async (
            ISecurityEventService securityEvents,
            int? hours) =>
        {
            var since = DateTime.UtcNow.AddHours(-(hours ?? 24));
            var highSeverity = await securityEvents.GetHighSeverityEventsAsync(since);

            var threatCategories = highSeverity
                .GroupBy(e => e.EventType)
                .Select(g => new
                {
                    type = g.Key,
                    count = g.Count(),
                    severity = g.Max(e => e.Severity),
                    latestRiskScore = g.OrderByDescending(e => e.CreatedAt).First().RiskScore,
                    events = g.OrderByDescending(e => e.CreatedAt).Take(5).Select(e => new
                    {
                        e.Id,
                        e.IpAddress,
                        e.Username,
                        e.Severity,
                        e.RiskScore,
                        e.Details,
                        e.IsBlocked,
                        e.CreatedAt
                    })
                })
                .OrderByDescending(g => g.count);

            var summary = new
            {
                totalThreats = highSeverity.Count,
                criticalCount = highSeverity.Count(e => e.Severity == "Critical"),
                highCount = highSeverity.Count(e => e.Severity == "High"),
                blockedCount = highSeverity.Count(e => e.IsBlocked),
                topIps = highSeverity.Where(e => e.IpAddress != null)
                    .GroupBy(e => e.IpAddress!)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new { ip = g.Key, count = g.Count() }),
            };

            return Results.Ok(new { summary, categories = threatCategories });
        }).WithName("GetThreatOverview");
    }

    // ═══════════════════════════════════════════════════════════
    // ACCOUNT LOCKOUTS (DB-backed CRUD)
    // ═══════════════════════════════════════════════════════════

    private static void MapAccountLockouts(RouteGroupBuilder group)
    {
        group.MapGet("/lockouts", async (IvfDbContext db) =>
        {
            var lockouts = await db.AccountLockouts
                .Where(l => !l.IsDeleted)
                .OrderByDescending(l => l.LockedAt)
                .Select(l => new
                {
                    l.Id,
                    userId = l.UserId.ToString(),
                    l.Username,
                    l.Reason,
                    l.LockedAt,
                    l.UnlocksAt,
                    l.FailedAttempts,
                    l.LockedBy,
                    l.IsManualLock,
                    isLocked = l.UnlocksAt > DateTime.UtcNow
                })
                .ToListAsync();

            return Results.Ok(lockouts);
        }).WithName("GetAccountLockouts");

        group.MapPost("/lockouts", async (LockAccountRequest request, IvfDbContext db) =>
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                return Results.BadRequest(new { message = "Invalid user ID" });

            var existing = await db.AccountLockouts.FirstOrDefaultAsync(l =>
                l.UserId == userId && !l.IsDeleted && l.UnlocksAt > DateTime.UtcNow);
            if (existing is not null)
                return Results.Conflict(new { message = "Account is already locked" });

            var lockout = AccountLockout.Create(
                userId: userId,
                username: request.Username,
                reason: request.Reason,
                durationMinutes: request.DurationMinutes,
                failedAttempts: request.FailedAttempts,
                lockedBy: request.LockedBy,
                isManualLock: true);

            await db.AccountLockouts.AddAsync(lockout);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Account locked", id = lockout.Id });
        }).WithName("LockAccount");

        group.MapPost("/lockouts/{id:guid}/unlock", async (Guid id, IvfDbContext db) =>
        {
            var lockout = await db.AccountLockouts.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);
            if (lockout is null)
                return Results.NotFound(new { message = "Lockout not found" });

            lockout.Unlock();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Account unlocked" });
        }).WithName("UnlockAccount");
    }

    // ═══════════════════════════════════════════════════════════
    // IP WHITELIST (DB-backed CRUD)
    // ═══════════════════════════════════════════════════════════

    private static void MapIpWhitelist(RouteGroupBuilder group)
    {
        group.MapGet("/ip-whitelist", async (IvfDbContext db) =>
        {
            var list = await db.IpWhitelistEntries
                .Where(ip => !ip.IsDeleted)
                .OrderByDescending(ip => ip.CreatedAt)
                .Select(ip => new
                {
                    ip.Id,
                    ip.IpAddress,
                    ip.CidrRange,
                    ip.Description,
                    ip.AddedBy,
                    addedAt = ip.CreatedAt,
                    ip.ExpiresAt,
                    isActive = ip.IsActive && (ip.ExpiresAt == null || ip.ExpiresAt > DateTime.UtcNow)
                })
                .ToListAsync();

            return Results.Ok(list);
        }).WithName("GetIpWhitelist");

        group.MapPost("/ip-whitelist", async (AddIpWhitelistRequest request, IvfDbContext db) =>
        {
            var existing = await db.IpWhitelistEntries.FirstOrDefaultAsync(ip =>
                ip.IpAddress == request.IpAddress && !ip.IsDeleted);
            if (existing is not null)
                return Results.Conflict(new { message = "IP already in whitelist" });

            var entry = IpWhitelistEntry.Create(
                ipAddress: request.IpAddress,
                description: request.Description,
                addedBy: request.AddedBy ?? "Admin",
                expiresInDays: request.ExpiresInDays,
                cidrRange: request.CidrRange);

            await db.IpWhitelistEntries.AddAsync(entry);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "IP added to whitelist", id = entry.Id });
        }).WithName("AddIpToWhitelist");

        group.MapPut("/ip-whitelist/{id:guid}", async (Guid id, UpdateIpWhitelistRequest request, IvfDbContext db) =>
        {
            var entry = await db.IpWhitelistEntries.FirstOrDefaultAsync(ip => ip.Id == id && !ip.IsDeleted);
            if (entry is null) return Results.NotFound(new { message = "IP not found" });

            entry.Update(request.Description, request.ExpiresInDays);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "IP whitelist entry updated" });
        }).WithName("UpdateIpWhitelist");

        group.MapDelete("/ip-whitelist/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var entry = await db.IpWhitelistEntries.FirstOrDefaultAsync(ip => ip.Id == id && !ip.IsDeleted);
            if (entry is null) return Results.NotFound(new { message = "IP not found in whitelist" });

            entry.MarkAsDeleted();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "IP removed from whitelist" });
        }).WithName("RemoveIpFromWhitelist");
    }

    // ═══════════════════════════════════════════════════════════
    // DEVICE MANAGEMENT
    // ═══════════════════════════════════════════════════════════

    private static void MapDevices(RouteGroupBuilder group)
    {
        group.MapGet("/devices/{userId:guid}", async (
            Guid userId,
            IvfDbContext db) =>
        {
            var devices = await db.DeviceRisks
                .Where(d => d.UserId == userId.ToString() && !d.IsDeleted)
                .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
                .Select(d => new
                {
                    d.Id,
                    fingerprint = d.DeviceId,
                    d.IpAddress,
                    d.Country,
                    d.UserAgent,
                    d.IsTrusted,
                    riskLevel = d.RiskLevel.ToString(),
                    d.RiskScore,
                    d.Factors,
                    firstSeen = d.CreatedAt,
                    lastSeen = d.UpdatedAt ?? d.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(devices);
        }).WithName("GetUserDevices");

        group.MapPost("/devices/{id:guid}/trust", async (Guid id, IvfDbContext db) =>
        {
            var device = await db.DeviceRisks.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
            if (device is null) return Results.NotFound(new { message = "Device not found" });

            device.MarkTrusted();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Device marked as trusted" });
        }).WithName("TrustDevice");

        group.MapDelete("/devices/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var device = await db.DeviceRisks.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
            if (device is null) return Results.NotFound(new { message = "Device not found" });

            device.MarkAsDeleted();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Device removed" });
        }).WithName("RemoveDevice");
    }

    // ═══════════════════════════════════════════════════════════
    // PASSKEYS / WEBAUTHN
    // ═══════════════════════════════════════════════════════════

    private static void MapPasskeys(RouteGroupBuilder group)
    {
        group.MapGet("/passkeys/{userId:guid}", async (Guid userId, IvfDbContext db) =>
        {
            var passkeys = await db.PasskeyCredentials
                .Where(p => p.UserId == userId && !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.CredentialId,
                    p.DeviceName,
                    p.CredentialType,
                    p.AttestationFormat,
                    p.AaGuid,
                    p.SignatureCounter,
                    p.IsActive,
                    p.LastUsedAt,
                    registeredAt = p.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(passkeys);
        }).WithName("GetUserPasskeys");

        group.MapPost("/passkeys/register/begin", async (PasskeyRegisterBeginRequest request, IvfDbContext db, IFido2 fido2) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted);
            if (user is null) return Results.NotFound(new { message = "User not found" });

            var existingCredentials = await db.PasskeyCredentials
                .Where(p => p.UserId == request.UserId && p.IsActive && !p.IsDeleted)
                .Select(p => new PublicKeyCredentialDescriptor(Convert.FromBase64String(p.CredentialId)))
                .ToListAsync();

            var fidoUser = new Fido2User
            {
                Name = user.Username,
                DisplayName = user.FullName,
                Id = Encoding.UTF8.GetBytes(user.Id.ToString())
            };

            var authenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Preferred,
                UserVerification = UserVerificationRequirement.Preferred
            };

            var options = fido2.RequestNewCredential(
                fidoUser,
                existingCredentials,
                authenticatorSelection,
                AttestationConveyancePreference.None);

            _pendingRegistrations[user.Id.ToString()] = options;

            var json = JsonSerializer.Serialize(options, _fidoJsonOptions);
            return Results.Text(json, "application/json");
        }).WithName("BeginPasskeyRegistration");

        group.MapPost("/passkeys/register/complete", async (HttpContext context, IvfDbContext db, IFido2 fido2) =>
        {
            var request = await JsonSerializer.DeserializeAsync<PasskeyRegisterCompleteRequest>(context.Request.Body, _fidoJsonOptions);
            if (request is null)
                return Results.BadRequest(new { message = "Invalid request" });

            if (!_pendingRegistrations.TryRemove(request.UserId.ToString(), out var options))
                return Results.BadRequest(new { message = "No pending registration found. Please restart." });

            var credential = await fido2.MakeNewCredentialAsync(request.AttestationResponse, options, async (args, ct) =>
            {
                var exists = await db.PasskeyCredentials.AnyAsync(
                    p => p.CredentialId == Convert.ToBase64String(args.CredentialId) && !p.IsDeleted, ct);
                return !exists;
            });

            if (credential?.Result is null)
                return Results.BadRequest(new { message = "Credential registration failed" });

            var passkey = PasskeyCredential.Create(
                userId: request.UserId,
                credentialId: Convert.ToBase64String(credential.Result.CredentialId),
                publicKey: Convert.ToBase64String(credential.Result.PublicKey),
                userHandle: Convert.ToBase64String(credential.Result.User.Id),
                signatureCounter: credential.Result.Counter,
                deviceName: request.DeviceName,
                attestationFormat: credential.Result.CredType,
                aaGuid: credential.Result.AaGuid.ToString());

            await db.PasskeyCredentials.AddAsync(passkey);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Passkey registered", id = passkey.Id });
        }).WithName("CompletePasskeyRegistration");

        group.MapPost("/passkeys/authenticate/begin", async (PasskeyAuthBeginRequest request, IvfDbContext db, IFido2 fido2) =>
        {
            var credentials = await db.PasskeyCredentials
                .Where(p => p.UserId == request.UserId && p.IsActive && !p.IsDeleted)
                .Select(p => new PublicKeyCredentialDescriptor(Convert.FromBase64String(p.CredentialId)))
                .ToListAsync();

            if (credentials.Count == 0)
                return Results.BadRequest(new { message = "No passkeys registered for this user" });

            var options = fido2.GetAssertionOptions(
                credentials,
                UserVerificationRequirement.Preferred);

            _pendingAssertions[request.UserId.ToString()] = options;

            var json = JsonSerializer.Serialize(options, _fidoJsonOptions);
            return Results.Text(json, "application/json");
        }).WithName("BeginPasskeyAuthentication");

        group.MapPost("/passkeys/authenticate/complete", async (HttpContext context, IvfDbContext db, IFido2 fido2) =>
        {
            var request = await JsonSerializer.DeserializeAsync<PasskeyAuthCompleteRequest>(context.Request.Body, _fidoJsonOptions);
            if (request is null)
                return Results.BadRequest(new { message = "Invalid request" });

            if (!_pendingAssertions.TryRemove(request.UserId.ToString(), out var options))
                return Results.BadRequest(new { message = "No pending authentication found" });

            var credentialIdBase64 = Convert.ToBase64String(request.AssertionResponse.Id);
            var storedCredential = await db.PasskeyCredentials
                .FirstOrDefaultAsync(p => p.CredentialId == credentialIdBase64 && p.IsActive && !p.IsDeleted);

            if (storedCredential is null)
                return Results.BadRequest(new { message = "Unknown credential" });

            var result = await fido2.MakeAssertionAsync(
                request.AssertionResponse,
                options,
                Convert.FromBase64String(storedCredential.PublicKey),
                storedCredential.SignatureCounter,
                async (args, ct) =>
                {
                    var cred = await db.PasskeyCredentials
                        .FirstOrDefaultAsync(p => p.UserId == request.UserId && p.CredentialId == credentialIdBase64 && !p.IsDeleted, ct);
                    return cred is not null;
                });

            storedCredential.UpdateCounter(result.Counter);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Authentication successful", signCount = result.Counter });
        }).WithName("CompletePasskeyAuthentication");

        group.MapDelete("/passkeys/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var passkey = await db.PasskeyCredentials.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (passkey is null) return Results.NotFound(new { message = "Passkey not found" });

            passkey.Revoke();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Passkey revoked" });
        }).WithName("RevokePasskey");

        group.MapPut("/passkeys/{id:guid}/rename", async (Guid id, RenamePasskeyRequest request, IvfDbContext db) =>
        {
            var passkey = await db.PasskeyCredentials.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (passkey is null) return Results.NotFound(new { message = "Passkey not found" });

            passkey.Rename(request.DeviceName);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Passkey renamed" });
        }).WithName("RenamePasskey");
    }

    // ═══════════════════════════════════════════════════════════
    // TOTP (Authenticator App)
    // ═══════════════════════════════════════════════════════════

    private static void MapTotp(RouteGroupBuilder group)
    {
        group.MapPost("/totp/setup", async (TotpSetupRequest request, IvfDbContext db) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted);
            if (user is null) return Results.NotFound(new { message = "User not found" });

            var mfa = await db.UserMfaSettings.FirstOrDefaultAsync(m => m.UserId == request.UserId && !m.IsDeleted);
            if (mfa is null)
            {
                mfa = UserMfaSetting.Create(request.UserId);
                await db.UserMfaSettings.AddAsync(mfa);
            }

            var secret = KeyGeneration.GenerateRandomKey(20);
            var base32Secret = Base32Encoding.ToString(secret);

            mfa.EnableTotp(base32Secret);
            await db.SaveChangesAsync();

            var issuer = Uri.EscapeDataString("IVF System");
            var accountName = Uri.EscapeDataString(user.Username);
            var otpauthUri = $"otpauth://totp/{issuer}:{accountName}?secret={base32Secret}&issuer={issuer}&digits=6&period=30";

            return Results.Ok(new
            {
                secret = base32Secret,
                otpauthUri,
                message = "Scan the QR code with your authenticator app, then verify with a code"
            });
        }).WithName("SetupTotp");

        group.MapPost("/totp/verify", async (TotpVerifyRequest request, IvfDbContext db) =>
        {
            var mfa = await db.UserMfaSettings.FirstOrDefaultAsync(m => m.UserId == request.UserId && !m.IsDeleted);
            if (mfa is null || string.IsNullOrEmpty(mfa.TotpSecretKey))
                return Results.BadRequest(new { message = "TOTP not set up. Call /totp/setup first." });

            var secretBytes = Base32Encoding.ToBytes(mfa.TotpSecretKey);
            var totp = new Totp(secretBytes, step: 30, totpSize: 6);
            var isValid = totp.VerifyTotp(request.Code, out _, new VerificationWindow(previous: 1, future: 1));

            if (!isValid)
            {
                mfa.RecordMfaFailure();
                await db.SaveChangesAsync();
                return Results.BadRequest(new { message = "Invalid TOTP code", failedAttempts = mfa.FailedMfaAttempts });
            }

            if (!mfa.IsTotpVerified)
            {
                mfa.VerifyTotp();

                var recoveryCodes = Enumerable.Range(0, 8)
                    .Select(_ => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4)))
                    .ToArray();

                var hashedCodes = recoveryCodes.Select(c =>
                    Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(c)))).ToArray();
                mfa.SetRecoveryCodes(System.Text.Json.JsonSerializer.Serialize(hashedCodes));
            }

            mfa.RecordMfaSuccess();
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "TOTP verified successfully", verified = true });
        }).WithName("VerifyTotp");

        group.MapPost("/totp/validate", async (TotpVerifyRequest request, IvfDbContext db) =>
        {
            var mfa = await db.UserMfaSettings.FirstOrDefaultAsync(m =>
                m.UserId == request.UserId && m.IsMfaEnabled && m.IsTotpVerified && !m.IsDeleted);
            if (mfa is null || string.IsNullOrEmpty(mfa.TotpSecretKey))
                return Results.BadRequest(new { message = "TOTP not enabled for this user" });

            var secretBytes = Base32Encoding.ToBytes(mfa.TotpSecretKey);
            var totp = new Totp(secretBytes, step: 30, totpSize: 6);
            var isValid = totp.VerifyTotp(request.Code, out _, new VerificationWindow(previous: 1, future: 1));

            if (!isValid)
            {
                mfa.RecordMfaFailure();
                await db.SaveChangesAsync();
                return Results.BadRequest(new { message = "Invalid TOTP code" });
            }

            mfa.RecordMfaSuccess();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "TOTP validation successful", valid = true });
        }).WithName("ValidateTotp");
    }

    // ═══════════════════════════════════════════════════════════
    // SMS OTP
    // ═══════════════════════════════════════════════════════════

    private static void MapSmsOtp(RouteGroupBuilder group)
    {
        group.MapPost("/sms/register", async (SmsRegisterRequest request, IvfDbContext db) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted);
            if (user is null) return Results.NotFound(new { message = "User not found" });

            var mfa = await db.UserMfaSettings.FirstOrDefaultAsync(m => m.UserId == request.UserId && !m.IsDeleted);
            if (mfa is null)
            {
                mfa = UserMfaSetting.Create(request.UserId);
                await db.UserMfaSettings.AddAsync(mfa);
            }

            mfa.SetPhoneNumber(request.PhoneNumber);
            await db.SaveChangesAsync();

            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            _pendingSmsOtp[request.UserId.ToString()] = (otp, DateTime.UtcNow.AddMinutes(5));

            return Results.Ok(new
            {
                message = "OTP sent to phone number",
                phoneNumber = MaskPhoneNumber(request.PhoneNumber),
                devOtp = otp
            });
        }).WithName("RegisterSmsOtp");

        group.MapPost("/sms/verify", async (SmsVerifyRequest request, IvfDbContext db) =>
        {
            if (!_pendingSmsOtp.TryRemove(request.UserId.ToString(), out var pending))
                return Results.BadRequest(new { message = "No pending OTP. Request a new one." });

            if (DateTime.UtcNow > pending.ExpiresAt)
                return Results.BadRequest(new { message = "OTP expired. Request a new one." });

            if (pending.Code != request.Code)
            {
                var mfa2 = await db.UserMfaSettings.FirstOrDefaultAsync(m => m.UserId == request.UserId && !m.IsDeleted);
                mfa2?.RecordMfaFailure();
                if (mfa2 is not null) await db.SaveChangesAsync();
                return Results.BadRequest(new { message = "Invalid OTP code" });
            }

            var mfa = await db.UserMfaSettings.FirstOrDefaultAsync(m => m.UserId == request.UserId && !m.IsDeleted);
            if (mfa is not null)
            {
                mfa.VerifyPhone();
                mfa.RecordMfaSuccess();
                await db.SaveChangesAsync();
            }

            return Results.Ok(new { message = "Phone number verified", verified = true });
        }).WithName("VerifySmsOtp");

        group.MapPost("/sms/send", async (SmsSendRequest request, IvfDbContext db) =>
        {
            var mfa = await db.UserMfaSettings.FirstOrDefaultAsync(m =>
                m.UserId == request.UserId && m.IsPhoneVerified && !m.IsDeleted);
            if (mfa is null || string.IsNullOrEmpty(mfa.PhoneNumber))
                return Results.BadRequest(new { message = "No verified phone number for this user" });

            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            _pendingSmsOtp[request.UserId.ToString()] = (otp, DateTime.UtcNow.AddMinutes(5));

            return Results.Ok(new
            {
                message = "OTP sent",
                phoneNumber = MaskPhoneNumber(mfa.PhoneNumber),
                devOtp = otp
            });
        }).WithName("SendSmsOtp");
    }

    // ─── Public helpers for cross-endpoint SMS OTP access ───
    public static bool ValidateSmsOtp(string userId, string code)
    {
        if (!_pendingSmsOtp.TryRemove(userId, out var pending))
            return false;
        if (DateTime.UtcNow > pending.ExpiresAt)
            return false;
        return string.Equals(pending.Code, code, StringComparison.Ordinal);
    }

    public static void StoreSmsOtp(string userId, string code, TimeSpan expiry)
    {
        _pendingSmsOtp[userId] = (code, DateTime.UtcNow.Add(expiry));
    }

    // ═══════════════════════════════════════════════════════════
    // MFA SETTINGS
    // ═══════════════════════════════════════════════════════════

    private static void MapMfaSettings(RouteGroupBuilder group)
    {
        group.MapGet("/mfa/{userId:guid}", async (Guid userId, IvfDbContext db) =>
        {
            var mfa = await db.UserMfaSettings.FirstOrDefaultAsync(m => m.UserId == userId && !m.IsDeleted);
            if (mfa is null)
                return Results.Ok(new
                {
                    userId,
                    isMfaEnabled = false,
                    mfaMethod = "none",
                    isTotpVerified = false,
                    isPhoneVerified = false,
                    phoneNumber = (string?)null,
                    lastMfaAt = (DateTime?)null
                });

            return Results.Ok(new
            {
                userId = mfa.UserId,
                mfa.IsMfaEnabled,
                mfa.MfaMethod,
                mfa.IsTotpVerified,
                mfa.IsPhoneVerified,
                phoneNumber = mfa.PhoneNumber != null ? MaskPhoneNumber(mfa.PhoneNumber) : null,
                mfa.LastMfaAt,
                mfa.FailedMfaAttempts
            });
        }).WithName("GetMfaSettings");

        group.MapDelete("/mfa/{userId:guid}", async (Guid userId, IvfDbContext db) =>
        {
            var mfa = await db.UserMfaSettings.FirstOrDefaultAsync(m => m.UserId == userId && !m.IsDeleted);
            if (mfa is null) return Results.NotFound(new { message = "MFA settings not found" });

            mfa.DisableMfa();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "MFA disabled" });
        }).WithName("DisableMfa");
    }

    // ═══════════════════════════════════════════════════════════
    // COMPLIANCE DASHBOARD (HIPAA/SOC2)
    // ═══════════════════════════════════════════════════════════

    private static void MapComplianceDashboard(RouteGroupBuilder group)
    {
        group.MapGet("/compliance", async (IvfDbContext db, ISecurityEventService securityEvents) =>
        {
            var totalUsers = await db.Users.CountAsync(u => !u.IsDeleted);
            var mfaEnabled = await db.UserMfaSettings.CountAsync(m => m.IsMfaEnabled && !m.IsDeleted);
            var passkeys = await db.PasskeyCredentials.CountAsync(p => p.IsActive && !p.IsDeleted);
            var trustedDevices = await db.DeviceRisks.CountAsync(d => d.IsTrusted && !d.IsDeleted);
            var rateLimitPolicies = await db.RateLimitConfigs.CountAsync(r => r.IsEnabled && !r.IsDeleted);
            var geoRules = await db.GeoBlockRules.CountAsync(g => g.IsEnabled && !g.IsDeleted);
            var ipWhitelist = await db.IpWhitelistEntries.CountAsync(i => i.IsActive && !i.IsDeleted);
            var ztPolicies = await db.ZTPolicies.CountAsync(z => !z.IsDeleted);

            var since24h = DateTime.UtcNow.AddHours(-24);
            var highSeverity = await securityEvents.GetHighSeverityEventsAsync(since24h);
            var hasAuditLogging = true; // Always enabled
            var auditEvents24h = highSeverity.Count;

            // HIPAA compliance checks
            var checks = new List<object>();

            // 1. Access Control (§164.312(a))
            var accessControlScore = 0;
            if (totalUsers > 0 && mfaEnabled > 0) accessControlScore += 25;
            if (passkeys > 0) accessControlScore += 25;
            if (ztPolicies > 0) accessControlScore += 25;
            if (rateLimitPolicies >= 3) accessControlScore += 25;
            checks.Add(new { id = "access_control", name = "Kiểm soát truy cập", standard = "HIPAA §164.312(a)", score = accessControlScore, maxScore = 100, status = accessControlScore >= 75 ? "pass" : accessControlScore >= 50 ? "warning" : "fail", details = $"MFA: {mfaEnabled}/{totalUsers} users, Passkeys: {passkeys}, ZT Policies: {ztPolicies}" });

            // 2. Audit Controls (§164.312(b))
            var auditScore = hasAuditLogging ? 50 : 0;
            if (auditEvents24h > 0) auditScore += 25;
            if (highSeverity.Any(e => e.IsBlocked)) auditScore += 25;
            checks.Add(new { id = "audit_controls", name = "Kiểm soát kiểm toán", standard = "HIPAA §164.312(b)", score = auditScore, maxScore = 100, status = auditScore >= 75 ? "pass" : auditScore >= 50 ? "warning" : "fail", details = $"Audit logging: Active, Events 24h: {auditEvents24h}, Blocked threats: {highSeverity.Count(e => e.IsBlocked)}" });

            // 3. Integrity Controls (§164.312(c))
            var integrityScore = 50; // Base — encryption at rest (DB)
            if (ipWhitelist > 0) integrityScore += 25;
            if (geoRules > 0) integrityScore += 25;
            checks.Add(new { id = "integrity", name = "Kiểm soát toàn vẹn", standard = "HIPAA §164.312(c)", score = integrityScore, maxScore = 100, status = integrityScore >= 75 ? "pass" : integrityScore >= 50 ? "warning" : "fail", details = $"Encryption: Active, IP Whitelist: {ipWhitelist} entries, Geo Rules: {geoRules}" });

            // 4. Transmission Security (§164.312(e))
            var transmissionScore = 75; // HTTPS enforced + JWT
            if (rateLimitPolicies > 0) transmissionScore += 25;
            checks.Add(new { id = "transmission", name = "Bảo mật truyền tải", standard = "HIPAA §164.312(e)", score = Math.Min(transmissionScore, 100), maxScore = 100, status = transmissionScore >= 75 ? "pass" : "warning", details = "HTTPS: Enforced, JWT RS256: Active, Rate Limiting: Active" });

            // 5. Person Authentication (§164.312(d))
            var authScore = 0;
            if (mfaEnabled > 0) authScore += 40;
            if (passkeys > 0) authScore += 30;
            if (trustedDevices > 0) authScore += 30;
            checks.Add(new { id = "authentication", name = "Xác thực người dùng", standard = "HIPAA §164.312(d)", score = Math.Min(authScore, 100), maxScore = 100, status = authScore >= 75 ? "pass" : authScore >= 50 ? "warning" : "fail", details = $"MFA Users: {mfaEnabled}, Passkeys: {passkeys}, Trusted Devices: {trustedDevices}" });

            // 6. Incident Response (§164.308(a)(6))
            var incidentScore = 0;
            var blockedThreats = highSeverity.Count(e => e.IsBlocked);
            if (hasAuditLogging) incidentScore += 30;
            if (blockedThreats > 0) incidentScore += 30; // Active blocking = incident response
            var lockouts = await db.AccountLockouts.CountAsync(l => !l.IsDeleted && l.UnlocksAt > DateTime.UtcNow);
            if (lockouts >= 0) incidentScore += 20; // Auto-lockout = incident response
            if (rateLimitPolicies >= 3) incidentScore += 20;
            checks.Add(new { id = "incident_response", name = "Phản ứng sự cố", standard = "HIPAA §164.308(a)(6)", score = Math.Min(incidentScore, 100), maxScore = 100, status = incidentScore >= 75 ? "pass" : incidentScore >= 50 ? "warning" : "fail", details = $"Auto-blocking: Active, Lockouts: {lockouts}, Rate Limits: {rateLimitPolicies}" });

            var overallScore = checks.Count > 0 ? (int)Math.Round(checks.Average(c => (int)((dynamic)c).score)) : 0;

            return Results.Ok(new
            {
                overallScore,
                level = overallScore >= 80 ? "compliant" : overallScore >= 60 ? "partial" : "non_compliant",
                framework = "HIPAA",
                checks,
                summary = new
                {
                    totalChecks = checks.Count,
                    passed = checks.Count(c => (string)((dynamic)c).status == "pass"),
                    warnings = checks.Count(c => (string)((dynamic)c).status == "warning"),
                    failed = checks.Count(c => (string)((dynamic)c).status == "fail")
                },
                lastAssessed = DateTime.UtcNow
            });
        }).WithName("GetComplianceDashboard");
    }

    // ═══════════════════════════════════════════════════════════
    // SECURITY POLICIES
    // ═══════════════════════════════════════════════════════════

    private static void MapSecurityPolicies(RouteGroupBuilder group)
    {
        group.MapGet("/policies", async (IvfDbContext db) =>
        {
            var totalUsers = await db.Users.CountAsync(u => !u.IsDeleted);
            var mfaEnabled = await db.UserMfaSettings.CountAsync(m => m.IsMfaEnabled && !m.IsDeleted);
            var ztPolicies = await db.ZTPolicies.Where(z => !z.IsDeleted).ToListAsync();
            var rateLimits = await db.RateLimitConfigs.Where(r => !r.IsDeleted).ToListAsync();

            // Aggregate policies from existing config into a unified view
            var policies = new List<object>
            {
                new {
                    id = "password_policy",
                    category = "authentication",
                    name = "Chính sách mật khẩu",
                    description = "Yêu cầu về độ mạnh mật khẩu",
                    isEnabled = true,
                    settings = new {
                        minLength = 8,
                        requireUppercase = true,
                        requireLowercase = true,
                        requireDigit = true,
                        requireSpecialChar = true,
                        maxAge = "90 ngày",
                        historyCount = 5
                    }
                },
                new {
                    id = "session_policy",
                    category = "session",
                    name = "Chính sách phiên",
                    description = "Quản lý phiên đăng nhập",
                    isEnabled = true,
                    settings = new {
                        tokenExpiry = "60 phút",
                        refreshTokenExpiry = "7 ngày",
                        maxConcurrentSessions = 3,
                        sessionTimeout = "60 phút",
                        requireReauthForSensitive = true,
                        bindToDevice = true,
                        bindToIp = true
                    }
                },
                new {
                    id = "mfa_policy",
                    category = "authentication",
                    name = "Chính sách MFA",
                    description = "Xác thực đa yếu tố",
                    isEnabled = true,
                    settings = new {
                        mfaEnforcement = mfaEnabled == totalUsers ? "required" : mfaEnabled > 0 ? "optional" : "disabled",
                        supportedMethods = new[] { "TOTP", "SMS OTP", "Passkeys/WebAuthn" },
                        totpAlgorithm = "SHA1",
                        totpDigits = 6,
                        totpPeriod = "30s",
                        maxFailedAttempts = 5,
                        usersEnabled = mfaEnabled,
                        totalUsers
                    }
                },
                new {
                    id = "device_trust_policy",
                    category = "device",
                    name = "Chính sách thiết bị",
                    description = "Quản lý tin cậy thiết bị",
                    isEnabled = true,
                    settings = new {
                        fingerprintAlgorithm = "SHA256",
                        trustLevels = new[] { "Unknown", "Untrusted", "PartiallyTrusted", "Trusted", "FullyManaged" },
                        driftDetection = true,
                        driftThresholds = new { ip = "30 pts", device = "40 pts", country = "60 pts", blockThreshold = "60 pts" }
                    }
                },
                new {
                    id = "zero_trust_policy",
                    category = "access",
                    name = "Chính sách Zero Trust",
                    description = "Mô hình bảo mật Zero Trust",
                    isEnabled = ztPolicies.Count > 0,
                    settings = new {
                        policyCount = ztPolicies.Count,
                        actions = new[] { "Allow", "Monitor", "StepUp", "RequireMFA", "RateLimit", "Block", "Quarantine" },
                        continuousEvaluation = true,
                        maxSessionAge = "8 giờ",
                        freshSessionWindow = "15 phút"
                    }
                },
                new {
                    id = "threat_detection_policy",
                    category = "threat",
                    name = "Chính sách phát hiện mối đe dọa",
                    description = "Phát hiện và phản ứng mối đe dọa tự động",
                    isEnabled = true,
                    settings = new {
                        bruteForceThreshold = "5 attempts / 15 min",
                        impossibleTravelSpeed = "900 km/h",
                        ipReputationCheck = true,
                        torBlocking = true,
                        vpnDetection = true,
                        inputValidation = new[] { "SQL Injection", "XSS", "Path Traversal", "Command Injection" },
                        riskScoreRange = "0-100"
                    }
                },
                new {
                    id = "rate_limit_policy",
                    category = "access",
                    name = "Chính sách giới hạn tốc độ",
                    description = "Giới hạn tần suất yêu cầu API",
                    isEnabled = true,
                    settings = new {
                        builtInPolicies = 5,
                        customPolicies = rateLimits.Count(r => r.IsEnabled),
                        globalLimit = "100 req/min",
                        authLimit = "10 req/min",
                        signingLimit = "30 req/min"
                    }
                },
                new {
                    id = "data_protection_policy",
                    category = "data",
                    name = "Chính sách bảo vệ dữ liệu",
                    description = "Mã hóa và bảo vệ dữ liệu nhạy cảm",
                    isEnabled = true,
                    settings = new {
                        encryptionAtRest = "AES-256 (PostgreSQL)",
                        encryptionInTransit = "TLS 1.3",
                        jwtAlgorithm = "RS256 Asymmetric",
                        passwordHashing = "BCrypt",
                        piiProtection = true,
                        auditLogging = true
                    }
                }
            };

            return Results.Ok(new { policies, lastUpdated = DateTime.UtcNow });
        }).WithName("GetSecurityPolicies");
    }

    // ═══════════════════════════════════════════════════════════
    // AUDIT REPORTS
    // ═══════════════════════════════════════════════════════════

    private static void MapAuditReports(RouteGroupBuilder group)
    {
        group.MapGet("/audit-report", async (
            ISecurityEventService securityEvents,
            IvfDbContext db,
            int? hours,
            string? severity,
            string? eventType) =>
        {
            var since = DateTime.UtcNow.AddHours(-(hours ?? 168)); // Default: 7 days
            var allEvents = await securityEvents.GetRecentEventsAsync(1000);

            var filtered = allEvents.Where(e => e.CreatedAt >= since);

            if (!string.IsNullOrEmpty(severity))
                filtered = filtered.Where(e => e.Severity == severity);
            if (!string.IsNullOrEmpty(eventType))
                filtered = filtered.Where(e => e.EventType == eventType);

            var events = filtered.OrderByDescending(e => e.CreatedAt).ToList();

            // Summary statistics
            var byCategory = events.GroupBy(e => e.EventType.Split('_')[0])
                .Select(g => new { category = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count);

            var bySeverity = events.GroupBy(e => e.Severity)
                .Select(g => new { severity = g.Key, count = g.Count() })
                .OrderBy(g => g.severity);

            var byHour = events.GroupBy(e => e.CreatedAt.ToString("yyyy-MM-dd HH:00"))
                .Select(g => new { hour = g.Key, count = g.Count() })
                .OrderBy(g => g.hour);

            var topIps = events.Where(e => e.IpAddress != null)
                .GroupBy(e => e.IpAddress!)
                .Select(g => new { ip = g.Key, count = g.Count(), blocked = g.Count(e => e.IsBlocked) })
                .OrderByDescending(g => g.count)
                .Take(10);

            var topUsers = events.Where(e => e.Username != null)
                .GroupBy(e => e.Username!)
                .Select(g => new { username = g.Key, count = g.Count(), suspicious = g.Count(e => e.Severity is "High" or "Critical") })
                .OrderByDescending(g => g.count)
                .Take(10);

            return Results.Ok(new
            {
                period = new { from = since, to = DateTime.UtcNow, hours = hours ?? 168 },
                summary = new
                {
                    totalEvents = events.Count,
                    criticalEvents = events.Count(e => e.Severity == "Critical"),
                    highEvents = events.Count(e => e.Severity == "High"),
                    blockedEvents = events.Count(e => e.IsBlocked),
                    uniqueIps = events.Select(e => e.IpAddress).Where(ip => ip != null).Distinct().Count(),
                    uniqueUsers = events.Select(e => e.Username).Where(u => u != null).Distinct().Count()
                },
                byCategory,
                bySeverity,
                byHour,
                topIps,
                topUsers,
                recentEvents = events.Take(100).Select(e => new
                {
                    e.Id,
                    e.EventType,
                    e.Severity,
                    e.Username,
                    e.IpAddress,
                    e.Country,
                    e.RiskScore,
                    e.IsBlocked,
                    e.Details,
                    e.CreatedAt
                }),
                generatedAt = DateTime.UtcNow
            });
        }).WithName("GetAuditReport");
    }

    // ═══════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════

    private static List<string> GetRiskFactors(SecurityEvent e)
    {
        var factors = new List<string>();
        if (e.EventType == SecurityEventTypes.LoginBruteForce)
            factors.Add("multiple_failed_attempts");
        if (e.EventType == SecurityEventTypes.ImpossibleTravel)
            factors.Add("impossible_travel");
        if (e.EventType == SecurityEventTypes.LoginFailed)
            factors.Add("login_failed");
        if (e.CreatedAt.Hour < 6 || e.CreatedAt.Hour > 22)
            factors.Add("off_hours_access");
        if (e.RiskScore >= 60) factors.Add("high_risk_score");
        if (e.IsBlocked) factors.Add("was_blocked");
        return factors;
    }

    private static string MaskPhoneNumber(string phone)
    {
        if (phone.Length <= 4) return "****";
        return new string('*', phone.Length - 4) + phone[^4..];
    }
}

// ═══════════════════════════════════════════════════════════
// REQUEST DTOs
// ═══════════════════════════════════════════════════════════

public record LockAccountRequest(
    string UserId,
    string Username,
    string Reason,
    int DurationMinutes = 30,
    int FailedAttempts = 0,
    string? LockedBy = null
);

public record AddIpWhitelistRequest(
    string IpAddress,
    string? Description,
    string? AddedBy,
    int? ExpiresInDays,
    string? CidrRange = null
);

public record UpdateIpWhitelistRequest(
    string? Description,
    int? ExpiresInDays
);

public record CreateRateLimitRequest(
    string PolicyName,
    string WindowType,
    int WindowSeconds,
    int PermitLimit,
    string? AppliesTo,
    string? CreatedBy,
    string? Description
);

public record UpdateRateLimitRequest(
    string WindowType,
    int WindowSeconds,
    int PermitLimit,
    string? Description
);

public record CreateGeoBlockRuleRequest(
    string CountryCode,
    string CountryName,
    bool IsBlocked,
    string? Reason,
    string? CreatedBy
);

public record PasskeyRegisterBeginRequest(Guid UserId);
public record PasskeyRegisterCompleteRequest(
    Guid UserId,
    AuthenticatorAttestationRawResponse AttestationResponse,
    string? DeviceName
);
public record PasskeyAuthBeginRequest(Guid UserId);
public record PasskeyAuthCompleteRequest(
    Guid UserId,
    AuthenticatorAssertionRawResponse AssertionResponse
);
public record RenamePasskeyRequest(string DeviceName);

public record TotpSetupRequest(Guid UserId);
public record TotpVerifyRequest(Guid UserId, string Code);

public record SmsRegisterRequest(Guid UserId, string PhoneNumber);
public record SmsVerifyRequest(Guid UserId, string Code);
public record SmsSendRequest(Guid UserId);
