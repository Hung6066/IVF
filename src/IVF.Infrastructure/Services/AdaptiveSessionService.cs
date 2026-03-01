using System.Collections.Concurrent;
using System.Security.Cryptography;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
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
/// </summary>
public sealed class AdaptiveSessionService : IAdaptiveSessionService
{
    private readonly IMemoryCache _cache;
    private readonly ISecurityEventService _securityEvents;
    private readonly ILogger<AdaptiveSessionService> _logger;

    private const string SessionPrefix = "session:";
    private const string UserSessionsPrefix = "user_sessions:";
    private static readonly TimeSpan DefaultSessionDuration = TimeSpan.FromHours(1);
    private const int MaxConcurrentSessions = 3;

    // Context drift thresholds
    private const decimal IpChangePenalty = 30m;
    private const decimal DeviceChangePenalty = 50m;
    private const decimal CountryChangePenalty = 60m;
    private const decimal DriftBlockThreshold = 50m;

    public AdaptiveSessionService(
        IMemoryCache cache,
        ISecurityEventService securityEvents,
        ILogger<AdaptiveSessionService> logger)
    {
        _cache = cache;
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

        _cache.Set($"{SessionPrefix}{sessionId}", session, DefaultSessionDuration);

        // Track user's active sessions
        var userSessions = _cache.GetOrCreate($"{UserSessionsPrefix}{userId}",
            entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromHours(8);
                return new ConcurrentDictionary<string, DateTime>();
            })!;

        userSessions[sessionId] = DateTime.UtcNow;

        // Enforce concurrent session limit
        if (userSessions.Count > MaxConcurrentSessions)
        {
            var oldestSession = userSessions
                .OrderBy(kvp => kvp.Value)
                .First();

            await RevokeSessionAsync(oldestSession.Key, "Exceeded concurrent session limit", ct);
            userSessions.TryRemove(oldestSession.Key, out _);

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

    public Task<SessionValidationResult> ValidateSessionAsync(string sessionId, RequestSecurityContext context, CancellationToken ct = default)
    {
        if (!_cache.TryGetValue($"{SessionPrefix}{sessionId}", out SessionInfo? session) || session is null)
        {
            return Task.FromResult(new SessionValidationResult(
                IsValid: false,
                ViolationReason: "Session not found or expired",
                IpChanged: false,
                DeviceChanged: false,
                CountryChanged: false,
                DriftScore: 100));
        }

        if (!session.IsActive || DateTime.UtcNow >= session.ExpiresAt)
        {
            return Task.FromResult(new SessionValidationResult(
                IsValid: false,
                ViolationReason: "Session expired or revoked",
                IpChanged: false,
                DeviceChanged: false,
                CountryChanged: false,
                DriftScore: 100));
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
            _cache.Set($"{SessionPrefix}{sessionId}", updatedSession, session.ExpiresAt - DateTime.UtcNow);
        }

        return Task.FromResult(new SessionValidationResult(
            IsValid: isValid,
            ViolationReason: violationReason,
            IpChanged: ipChanged,
            DeviceChanged: deviceChanged,
            CountryChanged: countryChanged,
            DriftScore: driftScore));
    }

    public async Task RevokeSessionAsync(string sessionId, string reason, CancellationToken ct = default)
    {
        if (_cache.TryGetValue($"{SessionPrefix}{sessionId}", out SessionInfo? session) && session is not null)
        {
            _cache.Remove($"{SessionPrefix}{sessionId}");

            // Remove from user's active sessions
            if (_cache.TryGetValue($"{UserSessionsPrefix}{session.UserId}", out ConcurrentDictionary<string, DateTime>? userSessions))
            {
                userSessions?.TryRemove(sessionId, out _);
            }

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

    public Task<List<SessionInfo>> GetActiveSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var sessions = new List<SessionInfo>();

        if (_cache.TryGetValue($"{UserSessionsPrefix}{userId}", out ConcurrentDictionary<string, DateTime>? userSessions) && userSessions is not null)
        {
            foreach (var kvp in userSessions)
            {
                if (_cache.TryGetValue($"{SessionPrefix}{kvp.Key}", out SessionInfo? session) && session is not null && session.IsActive)
                {
                    sessions.Add(session);
                }
            }
        }

        return Task.FromResult(sessions);
    }

    // ─── Private Helpers ───

    private static string GenerateSecureSessionId()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
