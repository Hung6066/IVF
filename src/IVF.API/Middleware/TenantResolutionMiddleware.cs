using IVF.Application.Common.Interfaces;
using IVF.Infrastructure.Persistence;

namespace IVF.API.Middleware;

/// <summary>
/// Resolves current tenant from JWT claims and sets it on DbContext for automatic row-level filtering.
/// Must run after authentication middleware.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var currentUser = context.RequestServices.GetService<ICurrentUserService>();
        var dbContext = context.RequestServices.GetService<IvfDbContext>();

        if (currentUser?.TenantId is { } tenantId && tenantId != Guid.Empty && dbContext is not null)
        {
            // Platform admins can optionally switch tenant via header
            if (currentUser.IsPlatformAdmin)
            {
                var headerTenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
                if (Guid.TryParse(headerTenantId, out var overrideTenantId))
                {
                    dbContext.SetCurrentTenant(overrideTenantId);
                }
                else
                {
                    // Platform admin without X-Tenant-Id sees all data (no filter)
                    // _currentTenantId stays null → query filter bypassed
                }
            }
            else
            {
                dbContext.SetCurrentTenant(tenantId);
            }
        }

        await _next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
