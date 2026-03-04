using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.API.Middleware;

/// <summary>
/// Zero Trust Continuous Verification Middleware.
///
/// Inspired by the highest-tier Zero Trust architectures:
/// - Google BeyondCorp: Every request is verified regardless of network origin
/// - Microsoft Zero Trust: Verify explicitly, least privilege, assume breach
/// - AWS Zero Trust: Context-aware, continuous verification, micro-segmentation
///
/// This middleware evaluates every request against multiple threat signals:
/// 1. Device fingerprint validation (BeyondCorp device inventory)
/// 2. Behavioral anomaly detection (GuardDuty/Sentinel)
/// 3. IP intelligence and reputation (Chronicle/GuardDuty)
/// 4. Session binding and drift detection (Conditional Access CAE)
/// 5. Input validation (WAF-level protection)
/// 6. Rate-based threat detection (Shield/DDoS)
///
/// The middleware enriches the request context with security metadata
/// and can block, step-up, or monitor based on risk scoring.
/// </summary>
public class ZeroTrustMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ZeroTrustMiddleware> _logger;

    // Paths that bypass zero trust evaluation (pre-auth endpoints)
    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/mfa-verify",
        "/api/auth/mfa-send-sms",
        "/api/auth/passkey-login",
        "/api/auth/login-biometric",
        "/api/auth/me",
        "/api/menu",
        "/api/permission-definitions",
        "/api/notifications",
        "/api/dashboard",
        "/api/compliance",
        "/api/ai",
        "/api/admin/certificates",
        "/api/trust",
        "/api/tenants",
        "/health",
        "/healthz",
        "/swagger"
    };

    // Sensitive paths that require elevated verification
    private static readonly HashSet<string> SensitivePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/zerotrust",
        "/api/keyvault",
        "/api/audit",
        "/api/users",
        "/api/signing-admin",
        "/api/backup",
        "/api/data-backup",
        "/api/cloud-replication",
        "/api/ca"
    };

    public ZeroTrustMiddleware(RequestDelegate next, ILogger<ZeroTrustMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IThreatDetectionService threatDetection,
        ISecurityEventService securityEvents,
        IDeviceFingerprintService deviceFingerprint,
        IAdaptiveSessionService sessionService,
        IConditionalAccessService conditionalAccess)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;

        // Skip evaluation for exempt paths
        if (IsExemptPath(path))
        {
            await _next(context);
            return;
        }

        // Skip for non-authenticated requests (let auth middleware handle)
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var correlationId = context.TraceIdentifier;

        // Build security context from the request
        var securityContext = BuildSecurityContext(context, correlationId);

        // 1. Threat Assessment — evaluate all signals
        var assessment = await threatDetection.AssessRequestAsync(securityContext);

        // Store assessment in HttpContext for downstream use
        context.Items["ZT_Assessment"] = assessment;
        context.Items["ZT_SecurityContext"] = securityContext;

        // 2. Add security response headers
        AddSecurityResponseHeaders(context, assessment, correlationId);

        // 3. Block if critical risk
        if (assessment.ShouldBlock)
        {
            await HandleBlockedRequest(context, assessment, securityContext, securityEvents, correlationId);
            return;
        }

        // 4. Elevated verification for sensitive paths
        if (IsSensitivePath(path) && assessment.RiskLevel >= RiskLevel.Medium)
        {
            await securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.ZtRiskElevated,
                severity: "Medium",
                userId: securityContext.UserId,
                username: securityContext.Username,
                ipAddress: securityContext.IpAddress,
                requestPath: path,
                requestMethod: method,
                riskScore: assessment.RiskScore,
                correlationId: correlationId,
                details: JsonSerializer.Serialize(new
                {
                    path,
                    riskLevel = assessment.RiskLevel.ToString(),
                    signals = assessment.Signals.Select(s => s.SignalType)
                })));
        }

        // 5. Conditional access policy evaluation
        var userRole = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        // DEBUG: log all claims to diagnose amr issue
        // _logger.LogWarning("ZT Claims for user {UserId}: {Claims}, AuthLevel={AuthLevel}",
        //     securityContext.UserId,
        //     string.Join(", ", context.User.Claims.Select(c => $"{c.Type}={c.Value}")),
        //     securityContext.CurrentAuthLevel);

        var deviceFpHeader = context.Request.Headers["X-Device-Fingerprint"].FirstOrDefault();
        DeviceTrustResult? deviceTrustResult = null;
        if (securityContext.UserId.HasValue && !string.IsNullOrEmpty(deviceFpHeader))
        {
            deviceTrustResult = await deviceFingerprint.CheckDeviceTrustAsync(securityContext.UserId.Value, deviceFpHeader);
        }
        var caResult = await conditionalAccess.EvaluateAsync(securityContext, userRole, deviceTrustResult);
        if (!caResult.IsAllowed)
        {
            await securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.ConditionalAccessBlocked,
                severity: caResult.Action == "Block" ? "High" : "Medium",
                userId: securityContext.UserId,
                username: securityContext.Username,
                ipAddress: securityContext.IpAddress,
                requestPath: path,
                requestMethod: method,
                isBlocked: caResult.Action == "Block",
                correlationId: correlationId,
                details: JsonSerializer.Serialize(new
                {
                    action = caResult.Action,
                    policyName = caResult.PolicyName,
                    policyId = caResult.PolicyId
                })));

            if (caResult.Action == "Block")
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = caResult.Message ?? "Access denied by security policy",
                    code = "CONDITIONAL_ACCESS_BLOCKED",
                    correlationId
                });
                return;
            }

            // For RequireMfa/RequireStepUp — return 401 with code so frontend can handle
            if (caResult.Action is "RequireMfa" or "RequireStepUp")
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = caResult.Message ?? "Additional authentication required",
                    code = $"CA_{caResult.Action.ToUpperInvariant()}",
                    correlationId
                });
                return;
            }
        }

        // 6. Session context validation (detect hijacking)
        if (!string.IsNullOrEmpty(securityContext.SessionId))
        {
            var sessionResult = await sessionService.ValidateSessionAsync(securityContext.SessionId, securityContext);
            if (!sessionResult.IsValid)
            {
                await securityEvents.LogEventAsync(SecurityEvent.Create(
                    eventType: SecurityEventTypes.SessionHijackAttempt,
                    severity: "Critical",
                    userId: securityContext.UserId,
                    username: securityContext.Username,
                    ipAddress: securityContext.IpAddress,
                    requestPath: path,
                    details: JsonSerializer.Serialize(new
                    {
                        reason = sessionResult.ViolationReason,
                        ipChanged = sessionResult.IpChanged,
                        deviceChanged = sessionResult.DeviceChanged,
                        countryChanged = sessionResult.CountryChanged,
                        driftScore = sessionResult.DriftScore
                    }),
                    isBlocked: true,
                    correlationId: correlationId));

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Session security violation detected",
                    code = "SESSION_CONTEXT_DRIFT",
                    correlationId
                });
                return;
            }
        }

        // 6. Log continuous verification event for monitoring
        if (assessment.RiskLevel >= RiskLevel.Medium)
        {
            await securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.ZtContinuousVerification,
                severity: assessment.RiskLevel >= RiskLevel.High ? "High" : "Medium",
                userId: securityContext.UserId,
                username: securityContext.Username,
                ipAddress: securityContext.IpAddress,
                requestPath: path,
                requestMethod: method,
                riskScore: assessment.RiskScore,
                correlationId: correlationId,
                threatIndicators: JsonSerializer.Serialize(assessment.Signals.Select(s => new { s.SignalType, s.Weight }))));
        }

        // Proceed with the request
        await _next(context);
    }

    // ─── Private Helpers ───

    private static RequestSecurityContext BuildSecurityContext(HttpContext context, string correlationId)
    {
        var userId = GetUserId(context);
        var username = context.User.Identity?.Name;
        var ipAddress = GetClientIp(context);
        var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();
        var deviceFp = context.Request.Headers["X-Device-Fingerprint"].FirstOrDefault();
        // Prefer session_id from JWT (set by TokenBindingMiddleware) over header
        var sessionId = context.Items["TokenSessionId"] as string
            ?? context.Request.Headers["X-Session-Id"].FirstOrDefault();

        // Determine auth level from JWT claims (amr = authentication methods reference, RFC 8176)
        // .NET maps inbound JWT "amr" → long URI, so check both forms
        var amrClaim = context.User.FindFirst("amr")?.Value
            ?? context.User.FindFirst("http://schemas.microsoft.com/claims/authnmethodsreferences")?.Value;
        var authLevel = amrClaim switch
        {
            "mfa" => AuthLevel.MFA,
            "bio" => AuthLevel.Biometric,
            _ => AuthLevel.Session
        };

        // Store impersonation context for audit logging downstream
        var impersonationClaim = context.User.FindFirst("impersonation")?.Value;
        if (impersonationClaim == "true")
        {
            var actSub = context.User.FindFirst("act_sub")?.Value;
            context.Items["IsImpersonation"] = true;
            context.Items["ImpersonationActorId"] = actSub;
        }

        return new RequestSecurityContext(
            UserId: userId,
            Username: username,
            IpAddress: ipAddress,
            UserAgent: userAgent,
            DeviceFingerprint: deviceFp,
            Country: null, // Would be enriched by GeoIP service
            City: null,
            RequestPath: context.Request.Path.Value ?? "",
            RequestMethod: context.Request.Method,
            SessionId: sessionId,
            CorrelationId: correlationId,
            Timestamp: DateTime.UtcNow,
            CurrentAuthLevel: authLevel);
    }

    private static Guid? GetUserId(HttpContext context)
    {
        var claim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static string GetClientIp(HttpContext context)
    {
        // Check for forwarded headers (reverse proxy / load balancer)
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            // Take the first IP (original client)
            var firstIp = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(firstIp))
                return firstIp;
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
            return realIp;

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static void AddSecurityResponseHeaders(HttpContext context, ThreatAssessment assessment, string correlationId)
    {
        context.Response.Headers.Append("X-ZT-Risk-Level", assessment.RiskLevel.ToString());
        context.Response.Headers.Append("X-ZT-Correlation-Id", correlationId);

        // Add step-up hint if needed
        if (assessment.RecommendedAction == ZeroTrustAction.RequireMfa ||
            assessment.RecommendedAction == ZeroTrustAction.RequireStepUp)
        {
            context.Response.Headers.Append("X-ZT-Step-Up-Required", "true");
        }
    }

    private async Task HandleBlockedRequest(
        HttpContext context,
        ThreatAssessment assessment,
        RequestSecurityContext securityContext,
        ISecurityEventService securityEvents,
        string correlationId)
    {
        _logger.LogWarning(
            "ZT Blocked: User={UserId}, IP={IpAddress}, Path={Path}, Risk={RiskScore}, Signals={Signals}",
            securityContext.UserId,
            securityContext.IpAddress,
            securityContext.RequestPath,
            assessment.RiskScore,
            string.Join(",", assessment.Signals.Select(s => s.SignalType)));

        await securityEvents.LogEventAsync(SecurityEvent.Create(
            eventType: SecurityEventTypes.ZtAccessDenied,
            severity: "Critical",
            userId: securityContext.UserId,
            username: securityContext.Username,
            ipAddress: securityContext.IpAddress,
            userAgent: securityContext.UserAgent,
            deviceFingerprint: securityContext.DeviceFingerprint,
            requestPath: securityContext.RequestPath,
            requestMethod: securityContext.RequestMethod,
            responseStatusCode: 403,
            riskScore: assessment.RiskScore,
            isBlocked: true,
            correlationId: correlationId,
            details: assessment.BlockReason,
            threatIndicators: JsonSerializer.Serialize(assessment.Signals.Select(s => new
            {
                s.SignalType,
                s.Description,
                s.Weight,
                s.Severity,
                Category = s.Category.ToString()
            }))));

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Access denied by Zero Trust policy",
            code = "ZT_ACCESS_DENIED",
            riskLevel = assessment.RiskLevel.ToString(),
            correlationId
        });
    }

    private static bool IsExemptPath(string path)
    {
        foreach (var exempt in ExemptPaths)
        {
            if (path.StartsWith(exempt, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsSensitivePath(string path)
    {
        foreach (var sensitive in SensitivePaths)
        {
            if (path.StartsWith(sensitive, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

/// <summary>
/// Extension method for registering the Zero Trust middleware.
/// </summary>
public static class ZeroTrustMiddlewareExtensions
{
    /// <summary>
    /// Adds Zero Trust continuous verification middleware.
    /// Place AFTER authentication/authorization middleware.
    /// Every authenticated request is evaluated against threat signals.
    /// </summary>
    public static IApplicationBuilder UseZeroTrust(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ZeroTrustMiddleware>();
    }
}
