using System.Security.Claims;
using Serilog.Context;

namespace IVF.API.Middleware;

/// <summary>
/// Pushes tenant, user, and request context into Serilog LogContext
/// so every log entry within the request pipeline is enriched.
/// Runs after authentication and tenant resolution.
/// </summary>
public class LogContextEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public LogContextEnrichmentMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var username = context.User.FindFirstValue(ClaimTypes.Name) ?? "anonymous";
        var role = context.User.FindFirstValue(ClaimTypes.Role) ?? "none";
        var tenantId = context.User.FindFirstValue("tenant_id") ?? "system";
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var requestId = context.TraceIdentifier;

        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("Username", username))
        using (LogContext.PushProperty("UserRole", role))
        using (LogContext.PushProperty("TenantId", tenantId))
        using (LogContext.PushProperty("ClientIp", clientIp))
        using (LogContext.PushProperty("RequestId", requestId))
        {
            await _next(context);
        }
    }
}

public static class LogContextEnrichmentMiddlewareExtensions
{
    public static IApplicationBuilder UseLogContextEnrichment(this IApplicationBuilder app)
        => app.UseMiddleware<LogContextEnrichmentMiddleware>();
}
