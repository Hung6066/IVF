using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;

namespace IVF.API.Middleware;

/// <summary>
/// Resolves current tenant from JWT claims or custom domain Host header,
/// and sets it on DbContext for automatic row-level filtering.
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
        else if (dbContext is not null)
        {
            // Try custom domain-based resolution for unauthenticated requests (login page, public API)
            var host = context.Request.Host.Host;
            if (!IsDefaultHost(host))
            {
                var tenantRepo = context.RequestServices.GetService<ITenantRepository>();
                if (tenantRepo is not null)
                {
                    var tenant = await tenantRepo.GetByCustomDomainAsync(host);
                    if (tenant is not null && tenant.CustomDomainStatus == CustomDomainStatus.Verified)
                    {
                        dbContext.SetCurrentTenant(tenant.Id);
                        context.Items["TenantId"] = tenant.Id;
                        context.Items["TenantSlug"] = tenant.Slug;
                    }
                }
            }
        }

        await _next(context);
    }

    private static bool IsDefaultHost(string host)
    {
        return host is "localhost" or "127.0.0.1"
            || host.EndsWith(".ivf.clinic", StringComparison.OrdinalIgnoreCase)
            || host == "ivf.clinic";
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
