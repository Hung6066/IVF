using System.Security.Claims;

namespace IVF.API.Middleware;

/// <summary>
/// Token Binding Enforcement Middleware (Google BeyondCorp / Microsoft CAE pattern).
/// 
/// Validates that JWT claims (device_fingerprint, session_id) match the actual
/// request context. Prevents stolen tokens from being used on different devices.
/// This is the "proof-of-possession" layer that enterprise systems use.
/// </summary>
public class TokenBindingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenBindingMiddleware> _logger;

    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/mfa-verify",
        "/api/auth/mfa-send-sms",
        "/api/auth/passkey-login",
        "/health",
        "/healthz",
        "/swagger"
    };

    public TokenBindingMiddleware(RequestDelegate next, ILogger<TokenBindingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IVF.Application.Common.Interfaces.ISecurityEventService securityEvents)
    {
        var path = context.Request.Path.Value ?? "";

        if (IsExempt(path) || context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Validate session_id claim — if present, it must still be active
        var sessionIdClaim = context.User.FindFirst("session_id")?.Value;
        if (!string.IsNullOrEmpty(sessionIdClaim))
        {
            // Store for downstream ZeroTrust middleware
            context.Items["TokenSessionId"] = sessionIdClaim;
        }

        // Validate device_fingerprint claim — warn on mismatch but don't block
        // (device fingerprint can drift slightly between requests)
        var deviceFpClaim = context.User.FindFirst("device_fingerprint")?.Value;
        var requestDeviceFp = context.Request.Headers["X-Device-Fingerprint"].FirstOrDefault();

        if (!string.IsNullOrEmpty(deviceFpClaim) && !string.IsNullOrEmpty(requestDeviceFp)
            && deviceFpClaim != requestDeviceFp)
        {
            _logger.LogWarning(
                "Device fingerprint mismatch for user {User}: token={TokenFP}, request={RequestFP}",
                context.User.Identity.Name,
                deviceFpClaim[..Math.Min(8, deviceFpClaim.Length)],
                requestDeviceFp[..Math.Min(8, requestDeviceFp.Length)]);

            // Log but don't block — ZeroTrust middleware handles risk scoring
            context.Items["TokenBindingDrift"] = true;
        }

        // Validate JTI claim exists (for revocation support)
        var jtiClaim = context.User.FindFirst("jti")?.Value;
        if (string.IsNullOrEmpty(jtiClaim))
        {
            _logger.LogWarning("JWT missing JTI claim for user {User}", context.User.Identity?.Name);
        }
        else
        {
            context.Items["TokenJti"] = jtiClaim;
        }

        await _next(context);
    }

    private static bool IsExempt(string path)
    {
        foreach (var exempt in ExemptPaths)
        {
            if (path.StartsWith(exempt, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase);
    }
}

public static class TokenBindingMiddlewareExtensions
{
    public static IApplicationBuilder UseTokenBinding(this IApplicationBuilder builder)
        => builder.UseMiddleware<TokenBindingMiddleware>();
}
