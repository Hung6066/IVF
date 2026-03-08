using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Adaptive session management inspired by:
/// - Microsoft Conditional Access: Continuous access evaluation (CAE)
/// - Google BeyondCorp: Session-level trust with context binding
/// - AWS IAM Identity Center: Session policies with drift detection
///
/// Binds sessions to security context (IP, device, location) and continuously
/// re-evaluates trust on every request. Detects session hijacking via context drift.
///
/// Uses IDistributedCache (Redis) for multi-replica consistency.
/// Falls back to IMemoryCache if distributed cache is unavailable.
/// </summary>
public sealed class AdaptiveSessionService : IAdaptiveSessionService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _localCache;
    private readonly ISecurityEventService _securityEvents;
    private readonly ILogger<AdaptiveSessionService> _logger;

    private const string SessionPrefix = "session:";
    private const string UserSessionsPrefix = "user_sessions:";
    private static readonly TimeSpan DefaultSessionDuration = TimeSpan.FromHours(1);
    private const int MaxConcurrentSessions = 3;

    // Context drift thresholds
    // Only block when MULTIPLE signals drift (e.g. IP + device = 70 > 60)
    // Single-factor drift (device alone = 40) should warn but not block
    private const decimal IpChangePenalty = 30m;
    private const decimal DeviceChangePenalty = 40m;
    private const decimal CountryChangePenalty = 60m;
    private const decimal DriftBlockThreshold = 60m;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public AdaptiveSessionService(
        IDistributedCache distributedCache,
        IMemoryCache localCache,
        ISecurityEventService securityEvents,
        ILogger<AdaptiveSessionService> logger)
    {
        _distributedCache = distributedCache;
        _localCache = localCache;
        _securityEvents = securityEvents;
        _logger = logger;
    }

    public async Task<SessionInfo> CreateSessionAsync(Guid userId, RequestSecurityContext context, CancellationToken ct = default)
    {
        var sessionId = GenerateSecureSessionId();

        var session = new SessionInfo(
            SessionId: sessionId,
            UserId: userId,
            IpAddress: context.IpAddress,
            DeviceFingerprint: context.DeviceFingerprint,
            Country: context.Country,
            CreatedAt: DateTime.UtcNow,
            LastActivityAt: DateTime.UtcNow,
            ExpiresAt: DateTime.UtcNow.Add(DefaultSessionDuration),
            IsActive: true);

        await SetSessionAsync($"{SessionPrefix}{sessionId}", session, DefaultSessionDuration, ct);

        // Track user's active sessions
        var userSessions = await GetUserSessionsAsync(userId, ct);
        userSessions[sessionId] = DateTime.UtcNow;
        await SetUserSessionsAsync(userId, userSessions, ct);

        // Enforce concurrent session limit
        if (userSessions.Count > MaxConcurrentSessions)
        {
            var oldestSession = userSessions
                .OrderBy(kvp => kvp.Value)
                .First();

            await RevokeSessionAsync(oldestSession.Key, "Exceeded concurrent session limit", ct);
            userSessions.TryRemove(oldestSession.Key, out _);
            await SetUserSessionsAsync(userId, userSessions, ct);

            await _securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.ConcurrentSession,
                severity: "Medium",
                userId: userId,
                ipAddress: context.IpAddress,
                details: $"{{\"revokedSession\":\"{oldestSession.Key}\",\"reason\":\"concurrent_limit\"}}"), ct);
        }

        await _securityEvents.LogEventAsync(SecurityEvent.Create(
            eventType: SecurityEventTypes.SessionCreated,
            severity: "Info",
            userId: userId,
            ipAddress: context.IpAddress,
            deviceFingerprint: context.DeviceFingerprint,
            sessionId: sessionId), ct);

        return session;
    }

    public async Task<SessionValidationResult> ValidateSessionAsync(string sessionId, RequestSecurityContext context, CancellationToken ct = default)
    {
        var session = await GetSessionAsync($"{SessionPrefix}{sessionId}", ct);
        if (session is null)
        {
            // Session not found in distributed cache — this is expected for first-time
            // requests or if cache was cleared. Re-create from JWT context.
            _logger.LogInformation(
                "Session {SessionId} not in cache, re-creating from JWT context for user {UserId}",
                sessionId, context.UserId);

            session = new SessionInfo(
                SessionId: sessionId,
                UserId: context.UserId ?? Guid.Empty,
                IpAddress: context.IpAddress,
                DeviceFingerprint: context.DeviceFingerprint,
                Country: context.Country,
                CreatedAt: DateTime.UtcNow,
                LastActivityAt: DateTime.UtcNow,
                ExpiresAt: DateTime.UtcNow.Add(DefaultSessionDuration),
                IsActive: true);

            await SetSessionAsync($"{SessionPrefix}{sessionId}", session, DefaultSessionDuration, ct);

            return new SessionValidationResult(
                IsValid: true,
                ViolationReason: null,
                IpChanged: false,
                DeviceChanged: false,
                CountryChanged: false,
                DriftScore: 0);
        }

        if (!session.IsActive || DateTime.UtcNow >= session.ExpiresAt)
        {
            return new SessionValidationResult(
                IsValid: false,
                ViolationReason: "Session expired or revoked",
                IpChanged: false,
                DeviceChanged: false,
                CountryChanged: false,
                DriftScore: 100);
        }

        // Calculate context drift
        var ipChanged = !string.Equals(session.IpAddress, context.IpAddress);
        var deviceChanged = !string.IsNullOrEmpty(session.DeviceFingerprint) &&
                            !string.IsNullOrEmpty(context.DeviceFingerprint) &&
                            !string.Equals(session.DeviceFingerprint, context.DeviceFingerprint);
        var countryChanged = !string.IsNullOrEmpty(session.Country) &&
                             !string.IsNullOrEmpty(context.Country) &&
                             !string.Equals(session.Country, context.Country, StringComparison.OrdinalIgnoreCase);

        var driftScore = 0m;
        if (ipChanged) driftScore += IpChangePenalty;
        if (deviceChanged) driftScore += DeviceChangePenalty;
        if (countryChanged) driftScore += CountryChangePenalty;

        var isValid = driftScore < DriftBlockThreshold;
        string? violationReason = null;

        if (!isValid)
        {
            violationReason = $"Session context drift detected (score={driftScore}): " +
                              $"IP={ipChanged}, Device={deviceChanged}, Country={countryChanged}";

            _logger.LogWarning(
                "Session hijack attempt detected: SessionId={SessionId}, User={UserId}, Drift={DriftScore}",
                sessionId, session.UserId, driftScore);
        }
        else
        {
            // Update last activity
            var updatedSession = session with { LastActivityAt = DateTime.UtcNow };
            var remaining = session.ExpiresAt - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
                await SetSessionAsync($"{SessionPrefix}{sessionId}", updatedSession, remaining, ct);
        }

        return new SessionValidationResult(
            IsValid: isValid,
            ViolationReason: violationReason,
            IpChanged: ipChanged,
            DeviceChanged: deviceChanged,
            CountryChanged: countryChanged,
            DriftScore: driftScore);
    }

    public async Task RevokeSessionAsync(string sessionId, string reason, CancellationToken ct = default)
    {
        var session = await GetSessionAsync($"{SessionPrefix}{sessionId}", ct);
        if (session is not null)
        {
            await _distributedCache.RemoveAsync($"{SessionPrefix}{sessionId}", ct);

            // Remove from user's active sessions
            var userSessions = await GetUserSessionsAsync(session.UserId, ct);
            userSessions.TryRemove(sessionId, out _);
            await SetUserSessionsAsync(session.UserId, userSessions, ct);

            _logger.LogInformation("Session revoked: SessionId={SessionId}, User={UserId}, Reason={Reason}",
                sessionId, session.UserId, reason);

            await _securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.TokenRevoked,
                severity: reason.Contains("hijack", StringComparison.OrdinalIgnoreCase) ? "Critical" : "Info",
                userId: session.UserId,
                sessionId: sessionId,
                details: $"{{\"reason\":\"{reason}\"}}"), ct);
        }
    }

    public async Task<List<SessionInfo>> GetActiveSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var sessions = new List<SessionInfo>();
        var userSessions = await GetUserSessionsAsync(userId, ct);

        foreach (var kvp in userSessions)
        {
            var session = await GetSessionAsync($"{SessionPrefix}{kvp.Key}", ct);
            if (session is not null && session.IsActive)
            {
                sessions.Add(session);
            }
        }

        return sessions;
    }

    // ─── Private Helpers ───

    private static string GenerateSecureSessionId()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    // ─── Distributed Cache Helpers ───

    private async Task SetSessionAsync(string key, SessionInfo session, TimeSpan expiry, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(session, JsonOptions);
            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write session to distributed cache, using local cache");
            _localCache.Set(key, session, expiry);
        }
    }

    private async Task<SessionInfo?> GetSessionAsync(string key, CancellationToken ct)
    {
        try
        {
            var json = await _distributedCache.GetStringAsync(key, ct);
            if (json is not null)
                return JsonSerializer.Deserialize<SessionInfo>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read session from distributed cache, trying local");
        }

        // Fallback to local cache
        return _localCache.TryGetValue(key, out SessionInfo? session) ? session : null;
    }

    private async Task<ConcurrentDictionary<string, DateTime>> GetUserSessionsAsync(Guid userId, CancellationToken ct)
    {
        var key = $"{UserSessionsPrefix}{userId}";
        try
        {
            var json = await _distributedCache.GetStringAsync(key, ct);
            if (json is not null)
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json, JsonOptions);
                return dict is not null ? new ConcurrentDictionary<string, DateTime>(dict) : new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read user sessions from distributed cache");
        }
        return new ConcurrentDictionary<string, DateTime>();
    }

    private async Task SetUserSessionsAsync(Guid userId, ConcurrentDictionary<string, DateTime> sessions, CancellationToken ct)
    {
        var key = $"{UserSessionsPrefix}{userId}";
        try
        {
            var json = JsonSerializer.Serialize(sessions, JsonOptions);
            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(8)
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write user sessions to distributed cache");
        }
    }
}
