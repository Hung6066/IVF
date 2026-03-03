using System.Security.Claims;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;

namespace IVF.API.Middleware;

/// <summary>
/// Consent Enforcement Middleware — GDPR/HIPAA compliance gate.
///
/// Checks that the authenticated user has granted the required data-processing
/// consents before allowing access to sensitive clinical endpoints.
///
/// Behaviour:
/// - Returns 403 with X-Consent-Required header listing missing consent types
/// - Skips auth/health/admin endpoints and non-authenticated requests
/// - Maps API paths → required consent types
/// </summary>
public class ConsentEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ConsentEnforcementMiddleware> _logger;

    private static readonly HashSet<string> ExemptPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth",
        "/api/menu",
        "/api/enterprise-users",   // enterprise user management
        "/api/user-consents",      // consent management itself
        "/api/notifications",
        "/api/queue",
        "/api/dashboard",
        "/health",
        "/healthz",
        "/swagger",
        "/hubs"
    };

    /// <summary>
    /// Maps path prefixes to the consent types required to access them.
    /// </summary>
    private static readonly Dictionary<string, string[]> PathConsentMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/api/patients"] = [ConsentTypes.DataProcessing, ConsentTypes.MedicalRecords],
        ["/api/couples"] = [ConsentTypes.DataProcessing, ConsentTypes.MedicalRecords],
        ["/api/treatment-cycles"] = [ConsentTypes.DataProcessing, ConsentTypes.MedicalRecords],
        ["/api/ultrasounds"] = [ConsentTypes.DataProcessing, ConsentTypes.MedicalRecords],
        ["/api/lab"] = [ConsentTypes.DataProcessing, ConsentTypes.MedicalRecords],
        ["/api/andrology"] = [ConsentTypes.DataProcessing, ConsentTypes.MedicalRecords],
        ["/api/semen-analysis"] = [ConsentTypes.DataProcessing, ConsentTypes.MedicalRecords],
        ["/api/embryos"] = [ConsentTypes.DataProcessing, ConsentTypes.MedicalRecords],
        ["/api/sperm-bank"] = [ConsentTypes.DataProcessing, ConsentTypes.BiometricData],
        ["/api/fingerprint"] = [ConsentTypes.BiometricData],
        ["/api/reports"] = [ConsentTypes.DataProcessing, ConsentTypes.Analytics],
        ["/api/forms"] = [ConsentTypes.DataProcessing],
    };

    public ConsentEnforcementMiddleware(RequestDelegate next, ILogger<ConsentEnforcementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConsentValidationService consentService)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip exempt paths
        if (IsExempt(path))
        {
            await _next(context);
            return;
        }

        // Skip if not authenticated
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Determine required consents for this path
        var requiredTypes = GetRequiredConsents(path);
        if (requiredTypes.Length == 0)
        {
            await _next(context);
            return;
        }

        // Parse user ID from claims
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? context.User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Consent middleware: could not parse userId from claim '{Claim}' on {Path}", userIdClaim, path);
            await _next(context);
            return;
        }

        var missing = await consentService.GetMissingConsentsAsync(userId, requiredTypes);

        _logger.LogInformation(
            "Consent check for {UserId} on {Path}: required=[{Required}], missing=[{Missing}]",
            userId, path, string.Join(",", requiredTypes), string.Join(",", missing));

        if (missing.Count > 0)
        {
            _logger.LogWarning(
                "Consent enforcement blocked {Method} {Path} for user {UserId}. Missing: {Missing}",
                context.Request.Method, path, userId, string.Join(", ", missing));

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers["X-Consent-Required"] = string.Join(",", missing);
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Required data consent not granted",
                code = "CONSENT_REQUIRED",
                missingConsents = missing,
                message = "Vui lòng cấp đồng ý xử lý dữ liệu trước khi truy cập tài nguyên này."
            });
            return;
        }

        await _next(context);
    }

    private static bool IsExempt(string path)
    {
        foreach (var prefix in ExemptPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string[] GetRequiredConsents(string path)
    {
        foreach (var (prefix, types) in PathConsentMap)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return types;
        }
        return [];
    }
}

public static class ConsentEnforcementMiddlewareExtensions
{
    /// <summary>
    /// Adds consent enforcement middleware.
    /// Place AFTER UseAuthorization so the user identity is available.
    /// </summary>
    public static IApplicationBuilder UseConsentEnforcement(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ConsentEnforcementMiddleware>();
    }
}
