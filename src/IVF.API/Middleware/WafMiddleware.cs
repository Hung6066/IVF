using System.Text;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using IVF.API.Hubs;

namespace IVF.API.Middleware;

public class WafMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WafMiddleware> _logger;

    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health", "/healthz", "/swagger", "/favicon.ico"
    };

    private static readonly HashSet<string> BodyMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH"
    };

    private const long MaxBodyInspectionSize = 1_048_576; // 1MB

    public WafMiddleware(RequestDelegate next, ILogger<WafMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip exempt paths
        if (ExemptPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            || path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var wafService = context.RequestServices.GetService<IWafService>();
        if (wafService is null)
        {
            await _next(context);
            return;
        }

        // Build request context
        var clientIp = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
            ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? context.Request.Headers["X-Real-IP"].FirstOrDefault()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        var country = context.Request.Headers["CF-IPCountry"].FirstOrDefault();
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();

        // Read body for POST/PUT/PATCH (if small enough)
        string? body = null;
        if (BodyMethods.Contains(context.Request.Method)
            && context.Request.ContentLength is > 0 and <= MaxBodyInspectionSize)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // Extract relevant headers
        var headers = new Dictionary<string, string>();
        foreach (var header in new[] { "Content-Type", "Content-Length", "User-Agent", "Referer", "Origin" })
        {
            if (context.Request.Headers.TryGetValue(header, out var val))
                headers[header] = val.ToString();
        }

        var wafContext = new WafRequestContext(
            clientIp, country, path, context.Request.Method,
            context.Request.QueryString.Value, userAgent,
            headers, body, correlationId);

        var result = await wafService.EvaluateRequestAsync(wafContext);

        // Always set WAF status header
        context.Response.Headers["X-WAF-Status"] = result.IsBlocked ? "blocked" : result.IsChallenge ? "challenge" : "pass";

        if (result.RuleName is not null)
            context.Response.Headers["X-WAF-Rule"] = result.RuleName;

        if (result.IsBlocked || result.IsChallenge || result.IsRateLimited || result.IsLogged)
        {
            // Record event
            wafService.RecordEvent(new WafEventData(
                result.RuleId, result.RuleName ?? "Unknown", WafRuleGroup.Custom, result.Action,
                clientIp, country, path, context.Request.Method,
                context.Request.QueryString.Value, userAgent,
                null, null,
                result.IsBlocked ? 403 : result.IsRateLimited ? 429 : null,
                headers.Count > 0 ? JsonSerializer.Serialize(headers) : null,
                correlationId, result.ProcessingTimeMs));

            // Broadcast block/challenge events via SignalR
            if (result.IsBlocked || result.IsChallenge || result.IsRateLimited)
            {
                _ = BroadcastWafEventAsync(context, result, clientIp, path);
            }
        }

        if (result.IsBlocked)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "WAF_BLOCKED",
                message = result.BlockMessage,
                rule = result.RuleName
            }));
            return;
        }

        if (result.IsChallenge)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "WAF_CHALLENGE",
                message = "Challenge required",
                challengeRequired = true,
                rule = result.RuleName
            }));
            return;
        }

        if (result.IsRateLimited)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "WAF_RATE_LIMITED",
                message = "Rate limit exceeded",
                rule = result.RuleName
            }));
            return;
        }

        await _next(context);
    }

    private static async Task BroadcastWafEventAsync(HttpContext context, WafEvaluationResult result, string clientIp, string path)
    {
        try
        {
            var hubContext = context.RequestServices.GetService<IHubContext<InfrastructureHub>>();
            if (hubContext is not null)
            {
                await hubContext.Clients.Group("infra-monitoring").SendAsync("WafEvent", new
                {
                    action = result.Action.ToString(),
                    rule = result.RuleName,
                    clientIp,
                    path,
                    timestamp = DateTime.UtcNow
                });
            }
        }
        catch
        {
            // Best-effort broadcast
        }
    }
}

public static class WafMiddlewareExtensions
{
    public static IApplicationBuilder UseWaf(this IApplicationBuilder app)
    {
        return app.UseMiddleware<WafMiddleware>();
    }
}
