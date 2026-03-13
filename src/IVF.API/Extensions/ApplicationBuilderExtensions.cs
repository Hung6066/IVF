using IVF.API.Endpoints;
using IVF.API.Middleware;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IVF.API.Extensions;

/// <summary>
/// Application builder extensions for enterprise middleware pipeline
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the enterprise middleware pipeline
    /// </summary>
    public static WebApplication UseEnterprisePipeline(this WebApplication app)
    {
        // Level 1: Exception Handling & Logging
        app.UseEnterpriseExceptionHandling();
        app.UseEnterpriseLogging();

        // Level 2: Security Headers & HTTPS
        app.UseEnterpriseSecurity();

        // Level 3: Health Checks (before auth)
        app.MapEnterpriseHealthChecks();

        // Level 4: Rate Limiting
        app.UseRateLimiter();

        // Level 5: Authentication & Authorization
        app.UseAuthentication();
        app.UseAuthorization();

        // Level 6: Response Compression
        app.UseResponseCompression();

        // Level 7: Custom Middleware
        app.UseEnterpriseMiddleware();

        // Level 8: Endpoints
        app.MapEnterpriseEndpoints();

        return app;
    }

    /// <summary>
    /// Enterprise exception handling with problem details
    /// </summary>
    public static WebApplication UseEnterpriseExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exceptionHandler = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                var exception = exceptionHandler?.Error;

                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                    ?? Guid.NewGuid().ToString();

                logger.LogError(exception,
                    "Unhandled exception. CorrelationId: {CorrelationId}, Path: {Path}",
                    correlationId, context.Request.Path);

                context.Response.StatusCode = exception switch
                {
                    IVF.Application.Common.Exceptions.ValidationException => StatusCodes.Status400BadRequest,
                    IVF.Application.Common.Exceptions.NotFoundException => StatusCodes.Status404NotFound,
                    IVF.Application.Common.Exceptions.ForbiddenAccessException => StatusCodes.Status403Forbidden,
                    UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                    OperationCanceledException => StatusCodes.Status499ClientClosedRequest,
                    _ => StatusCodes.Status500InternalServerError
                };

                context.Response.ContentType = "application/problem+json";

                var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = context.Response.StatusCode,
                    Title = GetErrorTitle(context.Response.StatusCode),
                    Detail = app.Environment.IsDevelopment() ? exception?.Message : null,
                    Instance = context.Request.Path,
                    Extensions =
                    {
                        ["correlationId"] = correlationId,
                        ["traceId"] = context.TraceIdentifier
                    }
                };

                if (exception is IVF.Application.Common.Exceptions.ValidationException validationEx)
                {
                    problemDetails.Extensions["errors"] = validationEx.Errors;
                }

                await context.Response.WriteAsJsonAsync(problemDetails);
            });
        });

        return app;
    }

    /// <summary>
    /// Enterprise logging with Serilog request logging
    /// </summary>
    public static WebApplication UseEnterpriseLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
                diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
                diagnosticContext.Set("CorrelationId",
                    httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? httpContext.TraceIdentifier);

                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
                    diagnosticContext.Set("TenantId", httpContext.User.FindFirst("tenant_id")?.Value);
                }
            };

            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            // Don't log health check requests
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (httpContext.Request.Path.StartsWithSegments("/health"))
                    return Serilog.Events.LogEventLevel.Verbose;

                if (ex != null)
                    return Serilog.Events.LogEventLevel.Error;

                if (httpContext.Response.StatusCode >= 500)
                    return Serilog.Events.LogEventLevel.Error;

                if (httpContext.Response.StatusCode >= 400)
                    return Serilog.Events.LogEventLevel.Warning;

                if (elapsed > 3000)
                    return Serilog.Events.LogEventLevel.Warning;

                return Serilog.Events.LogEventLevel.Information;
            };
        });

        return app;
    }

    /// <summary>
    /// Enterprise security headers and HTTPS redirection
    /// </summary>
    public static WebApplication UseEnterpriseSecurity(this WebApplication app)
    {
        // HTTPS Redirection (production only)
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            // Note: HTTPS redirection handled by Caddy reverse proxy
        }

        // Security Headers Middleware
        app.Use(async (context, next) =>
        {
            // Security Headers (supplement to Caddy)
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("X-XSS-Protection", "0");
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Append("Permissions-Policy",
                "camera=(), microphone=(), geolocation=(), payment=()");

            // Remove server header
            context.Response.Headers.Remove("Server");
            context.Response.Headers.Remove("X-Powered-By");

            // Add correlation ID
            var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                ?? Guid.NewGuid().ToString();
            context.Response.Headers.Append("X-Correlation-Id", correlationId);
            context.Response.Headers.Append("X-Request-Id", context.TraceIdentifier);

            await next();
        });

        // CORS
        app.UseCors();

        return app;
    }

    /// <summary>
    /// Enterprise custom middleware
    /// </summary>
    public static WebApplication UseEnterpriseMiddleware(this WebApplication app)
    {
        // Tenant Context
        app.UseMiddleware<TenantContextMiddleware>();

        // Security Enforcement (IP whitelist, geo-blocking)
        app.UseMiddleware<SecurityEnforcementMiddleware>();

        // Zero Trust (session validation, device fingerprint)
        app.UseMiddleware<ZeroTrustMiddleware>();

        // Audit Logging
        app.UseMiddleware<AuditLoggingMiddleware>();

        return app;
    }

    /// <summary>
    /// Maps all enterprise endpoints
    /// </summary>
    public static WebApplication MapEnterpriseEndpoints(this WebApplication app)
    {
        // Swagger (development only or gated)
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "IVF API v1");
                options.RoutePrefix = "swagger";
            });
        }

        // API Endpoints
        app.MapAuthEndpoints();
        app.MapPatientEndpoints();
        app.MapCoupleEndpoints();
        app.MapCycleEndpoints();
        app.MapEmbryoEndpoints();
        app.MapUltrasoundEndpoints();
        app.MapQueueEndpoints();
        app.MapFormEndpoints();
        app.MapBillingEndpoints();
        app.MapAndrologyEndpoints();
        app.MapAppointmentEndpoints();
        app.MapDigitalSigningEndpoints();
        app.MapComplianceEndpoints();
        app.MapBackupRestoreEndpoints();
        app.MapBackupComplianceEndpoints();
        app.MapKeyVaultEndpoints();
        app.MapInfrastructureEndpoints();
        app.MapAdvancedSecurityEndpoints();
        app.MapEnterpriseSecurityEndpoints();

        // SignalR Hubs
        app.MapHub<IVF.API.Hubs.QueueHub>("/hubs/queue");
        app.MapHub<IVF.API.Hubs.NotificationHub>("/hubs/notifications");
        app.MapHub<IVF.API.Hubs.FingerprintHub>("/hubs/fingerprint");
        app.MapHub<IVF.API.Hubs.BackupHub>("/hubs/backup");
        app.MapHub<IVF.API.Hubs.EvidenceHub>("/hubs/evidence");
        app.MapHub<IVF.API.Hubs.InfrastructureHub>("/hubs/infrastructure");

        // Metrics endpoint for Prometheus
        app.MapPrometheusScrapingEndpoint("/metrics");

        return app;
    }

    private static string GetErrorTitle(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        429 => "Too Many Requests",
        499 => "Client Closed Request",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        504 => "Gateway Timeout",
        _ => "Error"
    };
}

/// <summary>
/// Tenant context middleware for multi-tenancy
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract tenant from JWT claims or header
        var tenantId = context.User.FindFirst("tenant_id")?.Value
            ?? context.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        if (!string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out var tenantGuid))
        {
            context.Items["TenantId"] = tenantGuid;

            // Set PostgreSQL session variable for RLS
            using var scope = context.RequestServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<IVF.Infrastructure.Persistence.IvfDbContext>();
            if (dbContext != null)
            {
                try
                {
                    await dbContext.Database.ExecuteSqlRawAsync(
                        $"SET app.current_tenant_id = '{tenantGuid}'");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set tenant context in database");
                }
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Audit logging middleware
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;

        await _next(context);

        // Only audit write operations
        if (context.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
        {
            var duration = DateTime.UtcNow - startTime;
            var userId = context.User.FindFirst("sub")?.Value;
            var tenantId = context.Items["TenantId"]?.ToString();

            _logger.LogInformation(
                "AUDIT: {Method} {Path} by User {UserId} Tenant {TenantId} " +
                "Status {StatusCode} Duration {Duration}ms",
                context.Request.Method,
                context.Request.Path,
                userId ?? "anonymous",
                tenantId ?? "none",
                context.Response.StatusCode,
                duration.TotalMilliseconds);
        }
    }
}
