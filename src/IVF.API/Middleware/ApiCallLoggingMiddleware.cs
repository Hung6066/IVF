using System.Diagnostics;
using System.Security.Claims;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;

namespace IVF.API.Middleware;

public class ApiCallLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public ApiCallLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        try
        {
            var tenantIdClaim = context.User.FindFirstValue("tenant_id");
            if (!Guid.TryParse(tenantIdClaim, out var tenantId))
                return;

            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(userIdClaim, out var userId);
            var username = context.User.FindFirstValue(ClaimTypes.Name);

            var log = ApiCallLog.Create(
                tenantId,
                userId != Guid.Empty ? userId : null,
                username,
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString().Length > 500
                    ? context.Request.Headers.UserAgent.ToString()[..500]
                    : context.Request.Headers.UserAgent.ToString());

            using var scope = context.RequestServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            dbContext.ApiCallLogs.Add(log);
            await dbContext.SaveChangesAsync();
        }
        catch
        {
            // Don't let logging failures affect the request
        }
    }
}
