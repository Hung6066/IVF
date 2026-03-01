using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Centralized security event logging — inspired by AWS CloudTrail + Microsoft Sentinel.
/// All security-relevant events flow through this service for correlation and alerting.
/// </summary>
public interface ISecurityEventService
{
    Task LogEventAsync(SecurityEvent securityEvent, CancellationToken ct = default);
    Task<List<SecurityEvent>> GetRecentEventsAsync(int count = 50, CancellationToken ct = default);
    Task<List<SecurityEvent>> GetEventsByUserAsync(Guid userId, DateTime since, CancellationToken ct = default);
    Task<List<SecurityEvent>> GetEventsByIpAsync(string ipAddress, DateTime since, CancellationToken ct = default);
    Task<List<SecurityEvent>> GetHighSeverityEventsAsync(DateTime since, CancellationToken ct = default);
    Task<int> GetFailedLoginCountAsync(string identifier, TimeSpan window, CancellationToken ct = default);
}

/// <summary>
/// Threat detection engine — inspired by AWS GuardDuty + Google Chronicle + Microsoft Defender.
/// Performs real-time threat analysis on every request.
/// </summary>
public interface IThreatDetectionService
{
    /// <summary>
    /// Evaluates a request context for threats. Returns aggregated threat signals.
    /// </summary>
    Task<ThreatAssessment> AssessRequestAsync(RequestSecurityContext context, CancellationToken ct = default);

    /// <summary>
    /// Detects impossible travel (login from geographically distant locations in short time).
    /// </summary>
    Task<bool> DetectImpossibleTravelAsync(Guid userId, string ipAddress, string? country, CancellationToken ct = default);

    /// <summary>
    /// Checks if an IP is associated with known threats (Tor exits, botnets, etc.)
    /// </summary>
    Task<IpIntelligenceResult> CheckIpReputationAsync(string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Detects brute force attempts against a user or IP.
    /// </summary>
    Task<bool> DetectBruteForceAsync(string identifier, CancellationToken ct = default);

    /// <summary>
    /// Checks for anomalous access patterns (unusual time, endpoint, volume).
    /// </summary>
    Task<bool> DetectAnomalousAccessAsync(Guid userId, RequestSecurityContext context, CancellationToken ct = default);

    /// <summary>
    /// Scans request body/headers for injection attacks (SQL, XSS, command injection).
    /// </summary>
    InputValidationResult ValidateInput(string? input);
}

/// <summary>
/// Device fingerprinting and trust management — inspired by Google BeyondCorp.
/// Creates a unique, verifiable device identity from browser/client signals.
/// </summary>
public interface IDeviceFingerprintService
{
    /// <summary>
    /// Generates a device fingerprint from request headers and context.
    /// Uses a cryptographic hash of device signals — no PII stored.
    /// </summary>
    string GenerateFingerprint(DeviceSignals signals);

    /// <summary>
    /// Validates an existing fingerprint against current request signals.
    /// Returns true if the device identity is consistent.
    /// </summary>
    bool ValidateFingerprint(string existingFingerprint, DeviceSignals currentSignals);

    /// <summary>
    /// Registers a new device for a user and returns the fingerprint.
    /// </summary>
    Task<string> RegisterDeviceAsync(Guid userId, DeviceSignals signals, CancellationToken ct = default);

    /// <summary>
    /// Checks if a device is known and trusted for a user.
    /// </summary>
    Task<DeviceTrustResult> CheckDeviceTrustAsync(Guid userId, string fingerprint, CancellationToken ct = default);
}

/// <summary>
/// Adaptive session management — inspired by Microsoft Conditional Access.
/// Manages session lifecycle, binding, and continuous re-evaluation.
/// </summary>
public interface IAdaptiveSessionService
{
    /// <summary>
    /// Creates a session with bound security context (IP, device, location).
    /// </summary>
    Task<SessionInfo> CreateSessionAsync(Guid userId, RequestSecurityContext context, CancellationToken ct = default);

    /// <summary>
    /// Validates a session is still bound to the original security context.
    /// Detects session hijacking via context drift.
    /// </summary>
    Task<SessionValidationResult> ValidateSessionAsync(string sessionId, RequestSecurityContext context, CancellationToken ct = default);

    /// <summary>
    /// Terminates a session (logout, timeout, or security violation).
    /// </summary>
    Task RevokeSessionAsync(string sessionId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Gets active sessions for a user (for concurrent session detection).
    /// </summary>
    Task<List<SessionInfo>> GetActiveSessionsAsync(Guid userId, CancellationToken ct = default);
}

// ─── DTOs ───

public record RequestSecurityContext(
    Guid? UserId,
    string? Username,
    string IpAddress,
    string? UserAgent,
    string? DeviceFingerprint,
    string? Country,
    string? City,
    string RequestPath,
    string RequestMethod,
    string? SessionId,
    string? CorrelationId,
    DateTime Timestamp,
    AuthLevel CurrentAuthLevel = AuthLevel.Session,
    Dictionary<string, string>? AdditionalSignals = null
);

public record ThreatAssessment(
    decimal RiskScore,
    RiskLevel RiskLevel,
    List<ThreatSignal> Signals,
    ZeroTrustAction RecommendedAction,
    bool ShouldBlock,
    string? BlockReason,
    DateTime AssessedAt
);

public record ThreatSignal(
    string SignalType,
    string Description,
    decimal Weight,
    string Severity,
    ThreatCategory Category
);

public record IpIntelligenceResult(
    string IpAddress,
    bool IsTor,
    bool IsVpn,
    bool IsProxy,
    bool IsHosting,
    bool IsKnownAttacker,
    string? Country,
    string? City,
    string? Isp,
    decimal ThreatScore,
    string? ThreatType
);

public record DeviceSignals(
    string? UserAgent,
    string? AcceptLanguage,
    string? AcceptEncoding,
    string IpAddress,
    string? ScreenResolution,
    string? Timezone,
    string? Platform,
    bool? CookiesEnabled,
    bool? DoNotTrack,
    string? ClientHints
);

public record DeviceTrustResult(
    bool IsKnown,
    bool IsTrusted,
    DeviceTrustLevel TrustLevel,
    DateTime? FirstSeen,
    DateTime? LastSeen,
    int AccessCount
);

public record SessionInfo(
    string SessionId,
    Guid UserId,
    string IpAddress,
    string? DeviceFingerprint,
    string? Country,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    DateTime ExpiresAt,
    bool IsActive
);

public record SessionValidationResult(
    bool IsValid,
    string? ViolationReason,
    bool IpChanged,
    bool DeviceChanged,
    bool CountryChanged,
    decimal DriftScore
);

public record InputValidationResult(
    bool IsClean,
    List<string> DetectedThreats,
    string? SanitizedInput
);
