using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Records security-relevant events for Zero Trust continuous monitoring.
/// Inspired by Google BeyondCorp, Microsoft Sentinel, and AWS GuardDuty event models.
/// Immutable after creation — no updates allowed.
/// </summary>
public class SecurityEvent : BaseEntity
{
    public string EventType { get; private set; } = string.Empty;
    public string Severity { get; private set; } = "Info"; // Info, Low, Medium, High, Critical
    public Guid? UserId { get; private set; }
    public string? Username { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? DeviceFingerprint { get; private set; }
    public string? Country { get; private set; }
    public string? City { get; private set; }
    public string? RequestPath { get; private set; }
    public string? RequestMethod { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public string? Details { get; private set; } // JSON with event-specific data
    public string? ThreatIndicators { get; private set; } // JSON array of threat signals
    public decimal? RiskScore { get; private set; }
    public bool IsBlocked { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? SessionId { get; private set; }

    private SecurityEvent() { }

    public static SecurityEvent Create(
        string eventType,
        string severity,
        Guid? userId = null,
        string? username = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceFingerprint = null,
        string? country = null,
        string? city = null,
        string? requestPath = null,
        string? requestMethod = null,
        int? responseStatusCode = null,
        string? details = null,
        string? threatIndicators = null,
        decimal? riskScore = null,
        bool isBlocked = false,
        string? correlationId = null,
        string? sessionId = null)
    {
        return new SecurityEvent
        {
            EventType = eventType,
            Severity = severity,
            UserId = userId,
            Username = username,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceFingerprint = deviceFingerprint,
            Country = country,
            City = city,
            RequestPath = requestPath,
            RequestMethod = requestMethod,
            ResponseStatusCode = responseStatusCode,
            Details = details,
            ThreatIndicators = threatIndicators,
            RiskScore = riskScore,
            IsBlocked = isBlocked,
            CorrelationId = correlationId,
            SessionId = sessionId
        };
    }
}

/// <summary>
/// Standard security event types aligned with MITRE ATT&CK and NIST CSF.
/// </summary>
public static class SecurityEventTypes
{
    // Authentication events
    public const string LoginSuccess = "AUTH_LOGIN_SUCCESS";
    public const string LoginFailed = "AUTH_LOGIN_FAILED";
    public const string LoginBruteForce = "AUTH_BRUTE_FORCE";
    public const string TokenRefresh = "AUTH_TOKEN_REFRESH";
    public const string TokenRevoked = "AUTH_TOKEN_REVOKED";
    public const string SessionExpired = "AUTH_SESSION_EXPIRED";
    public const string MfaRequired = "AUTH_MFA_REQUIRED";
    public const string MfaFailed = "AUTH_MFA_FAILED";

    // Authorization events
    public const string AccessDenied = "AUTHZ_ACCESS_DENIED";
    public const string PrivilegeEscalation = "AUTHZ_PRIVILEGE_ESCALATION";
    public const string PolicyViolation = "AUTHZ_POLICY_VIOLATION";

    // Zero Trust events
    public const string ZtAccessDenied = "ZT_ACCESS_DENIED";
    public const string ZtBreakGlass = "ZT_BREAK_GLASS";
    public const string ZtDeviceUntrusted = "ZT_DEVICE_UNTRUSTED";
    public const string ZtGeoFenceViolation = "ZT_GEO_FENCE_VIOLATION";
    public const string ZtRiskElevated = "ZT_RISK_ELEVATED";
    public const string ZtStepUpRequired = "ZT_STEP_UP_REQUIRED";
    public const string ZtContinuousVerification = "ZT_CONTINUOUS_VERIFICATION";

    // Threat detection events
    public const string ImpossibleTravel = "THREAT_IMPOSSIBLE_TRAVEL";
    public const string AnomalousAccess = "THREAT_ANOMALOUS_ACCESS";
    public const string SuspiciousUserAgent = "THREAT_SUSPICIOUS_UA";
    public const string TorExit = "THREAT_TOR_EXIT";
    public const string VpnProxy = "THREAT_VPN_PROXY";
    public const string RateLimitExceeded = "THREAT_RATE_LIMIT";
    public const string SqlInjectionAttempt = "THREAT_SQL_INJECTION";
    public const string XssAttempt = "THREAT_XSS_ATTEMPT";
    public const string PathTraversal = "THREAT_PATH_TRAVERSAL";
    public const string CommandInjection = "THREAT_COMMAND_INJECTION";

    // Device events
    public const string DeviceRegistered = "DEVICE_REGISTERED";
    public const string DeviceTrusted = "DEVICE_TRUSTED";
    public const string DeviceRevoked = "DEVICE_REVOKED";
    public const string DeviceFingerprintChanged = "DEVICE_FINGERPRINT_CHANGED";
    public const string NewDeviceLogin = "DEVICE_NEW_LOGIN";

    // Data events
    public const string SensitiveDataAccess = "DATA_SENSITIVE_ACCESS";
    public const string BulkDataExport = "DATA_BULK_EXPORT";
    public const string DataModification = "DATA_MODIFICATION";

    // Session events
    public const string SessionCreated = "SESSION_CREATED";
    public const string SessionHijackAttempt = "SESSION_HIJACK_ATTEMPT";
    public const string ConcurrentSession = "SESSION_CONCURRENT";
    public const string SessionAnomalous = "SESSION_ANOMALOUS";

    // API events
    public const string ApiKeyAbuse = "API_KEY_ABUSE";
    public const string ApiRateLimited = "API_RATE_LIMITED";
    public const string ApiUnauthorized = "API_UNAUTHORIZED";
}
