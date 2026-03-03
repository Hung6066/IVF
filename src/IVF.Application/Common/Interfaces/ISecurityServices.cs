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

// ─── Phase 1: Adaptive Authentication ───

/// <summary>
/// Risk-based step-up authentication — evaluates whether additional authentication factors
/// are required based on threat assessment context.
/// Inspired by Google Advanced Protection + Microsoft Entra risk-based policies.
/// </summary>
public interface IStepUpAuthService
{
    /// <summary>
    /// Evaluates whether step-up authentication is required based on the current threat assessment.
    /// Returns the required authentication level and reason.
    /// </summary>
    Task<StepUpDecision> EvaluateStepUpAsync(
        Guid userId,
        ThreatAssessment assessment,
        DeviceTrustResult deviceTrust,
        CancellationToken ct = default);
}

public record StepUpDecision(
    bool RequiresStepUp,
    string? RequiredAction, // "mfa", "mfa_and_device_verify", "block"
    string? Reason,
    decimal RiskScore
);

/// <summary>
/// Contextual authentication — evaluates login context (new device, unusual time, new country)
/// to determine if additional verification is needed.
/// </summary>
public interface IContextualAuthService
{
    /// <summary>
    /// Evaluates the login context against the user's behavior profile.
    /// Returns triggers that require additional verification.
    /// </summary>
    Task<ContextualAuthResult> EvaluateContextAsync(
        Guid userId,
        string ipAddress,
        string? userAgent,
        string? deviceFingerprint,
        string? country,
        CancellationToken ct = default);
}

public record ContextualAuthResult(
    bool RequiresAdditionalVerification,
    List<string> Triggers, // "new_device", "unusual_time", "new_country", "long_absence"
    string? RecommendedAction // "mfa", "email_verification", "security_question"
);

/// <summary>
/// Conditional access policy evaluator — checks all active policies against request context.
/// Inspired by Microsoft Entra Conditional Access + Google BeyondCorp Access.
/// </summary>
public interface IConditionalAccessService
{
    /// <summary>
    /// Evaluates all active conditional access policies against the current request context.
    /// Returns the most restrictive applicable action.
    /// </summary>
    Task<ConditionalAccessResult> EvaluateAsync(
        RequestSecurityContext context,
        string? userRole,
        DeviceTrustResult? deviceTrust,
        CancellationToken ct = default);
}

public record ConditionalAccessResult(
    bool IsAllowed,
    string Action, // "Allow", "Block", "RequireMfa", "RequireStepUp", "RequireDeviceCompliance"
    string? PolicyName,
    string? Message,
    Guid? PolicyId
);

// ─── Phase 3: Threat Detection & Response ───

/// <summary>
/// Behavioral analytics engine — builds and queries user behavior profiles
/// for anomaly detection in the authentication flow.
/// </summary>
public interface IBehavioralAnalyticsService
{
    /// <summary>
    /// Builds/updates the behavior profile for a user based on login history.
    /// </summary>
    Task UpdateProfileAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Checks the current login context against the user's established behavior profile.
    /// Returns anomaly flags and confidence scores.
    /// </summary>
    Task<BehaviorAnalysisResult> AnalyzeLoginAsync(
        Guid userId,
        string ipAddress,
        string? userAgent,
        string? country,
        DateTime loginTime,
        CancellationToken ct = default);
}

public record BehaviorAnalysisResult(
    bool IsAnomalous,
    decimal AnomalyScore, // 0-100
    List<string> AnomalyFactors // "unusual_time", "new_ip", "new_country", "new_device", "rapid_succession"
);

/// <summary>
/// Incident response automation — evaluates security events against rules
/// and executes automated response actions.
/// </summary>
public interface IIncidentResponseService
{
    /// <summary>
    /// Evaluates a security event against all active incident response rules.
    /// Creates incidents and executes automated actions when rules match.
    /// </summary>
    Task ProcessEventAsync(SecurityEvent securityEvent, CancellationToken ct = default);
}

/// <summary>
/// Bot detection service — analyzes requests for automated/bot behavior.
/// Integrates reCAPTCHA v3 for score-based invisible verification.
/// </summary>
public interface IBotDetectionService
{
    /// <summary>
    /// Validates a reCAPTCHA token and returns the bot risk score.
    /// </summary>
    Task<BotDetectionResult> ValidateCaptchaAsync(string token, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Analyzes request signals for bot-like behavior (timing, headers, patterns).
    /// </summary>
    BotDetectionResult AnalyzeRequest(RequestSecurityContext context);
}

public record BotDetectionResult(
    bool IsBot,
    decimal Score, // 0.0 = bot, 1.0 = human (reCAPTCHA convention)
    string? Reason
);

// ─── Phase 4: Privacy & Compliance ───

/// <summary>
/// Data retention service — executes automated purging based on configured policies.
/// </summary>
public interface IDataRetentionService
{
    /// <summary>
    /// Executes all active data retention policies. Called by background service on schedule.
    /// </summary>
    Task<DataRetentionResult> ExecutePoliciesAsync(CancellationToken ct = default);
}

public record DataRetentionResult(
    int PoliciesExecuted,
    int TotalRecordsPurged,
    List<string> Errors
);

/// <summary>
/// Secrets scanner — detects leaked credentials in log entries and security event data.
/// </summary>
public interface ISecretsScanner
{
    /// <summary>
    /// Scans text content for potential secrets (JWT tokens, API keys, passwords, connection strings).
    /// </summary>
    SecretsScanResult Scan(string? content);
}

public record SecretsScanResult(
    bool HasSecrets,
    List<string> DetectedTypes // "jwt_token", "api_key", "connection_string", "private_key"
);

// ─── Phase 5: Session & Attestation ───

/// <summary>
/// GeoIP location service — resolves IP addresses to geographic coordinates.
/// </summary>
public interface IGeoLocationService
{
    /// <summary>
    /// Resolves an IP address to a geographic location.
    /// </summary>
    GeoLocation? Resolve(string ipAddress);

    /// <summary>
    /// Calculates distance between two geographic points in kilometers (Haversine formula).
    /// </summary>
    double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2);
}

public record GeoLocation(
    string? Country,
    string? CountryCode,
    string? Region,
    string? City,
    double Latitude,
    double Longitude,
    string? Isp
);

// ─── Phase 6: Security Notifications ───

/// <summary>
/// Security-specific notification service — sends security alerts through configured channels
/// (in-app via SignalR, email, SMS). Distinct from generic INotificationService.
/// </summary>
public interface ISecurityNotificationService
{
    /// <summary>
    /// Sends a security notification to a user through their preferred channels.
    /// </summary>
    Task SendSecurityAlertAsync(
        Guid userId,
        string eventType,
        string message,
        CancellationToken ct = default);

    /// <summary>
    /// Sends an alert to all admin users.
    /// </summary>
    Task SendAdminAlertAsync(
        string eventType,
        string message,
        CancellationToken ct = default);
}

/// <summary>
/// Breached password checker — validates passwords against known breach databases
/// using k-anonymity (Have I Been Pwned API).
/// </summary>
public interface IBreachedPasswordService
{
    /// <summary>
    /// Checks if a password has appeared in known data breaches.
    /// Uses k-anonymity: only the first 5 chars of SHA-1 hash are sent.
    /// </summary>
    Task<BreachedPasswordResult> CheckAsync(string password, CancellationToken ct = default);
}

public record BreachedPasswordResult(
    bool IsBreached,
    int BreachCount // Number of times seen in breaches
);
