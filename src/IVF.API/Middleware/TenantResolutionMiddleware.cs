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
                    // Platform admin without X-Tenant-Id falls back to their own (root) tenant
                    dbContext.SetCurrentTenant(tenantId);
                }
            }
            else
            {
                // Check tenant status — block suspended/cancelled tenants
                var tenantRepo = context.RequestServices.GetService<ITenantRepository>();
                if (tenantRepo is not null)
                {
                    var tenant = await tenantRepo.GetByIdAsync(tenantId);
                    if (tenant is null || tenant.Status == TenantStatus.Suspended || tenant.Status == TenantStatus.Cancelled)
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            code = "TENANT_SUSPENDED",
                            error = "Trung tâm của bạn đã bị tạm ngưng hoạt động. Vui lòng liên hệ quản trị viên."
                        });
                        return;
                    }
                }

                dbContext.SetCurrentTenant(tenantId);
            }
        }
        else if (dbContext is not null)
        {
            var host = context.Request.Host.Host;
            var slug = ExtractSubdomainSlug(host);

            if (slug is not null)
            {
                // Subdomain-based resolution: {slug}.ivf.clinic → tenant by slug
                var tenantRepo = context.RequestServices.GetService<ITenantRepository>();
                if (tenantRepo is not null)
                {
                    var tenant = await tenantRepo.GetBySlugAsync(slug);
                    if (tenant is not null && tenant.Status == TenantStatus.Active)
                    {
                        dbContext.SetCurrentTenant(tenant.Id);
                        context.Items["TenantId"] = tenant.Id;
                        context.Items["TenantSlug"] = tenant.Slug;
                    }
                }
            }
            else if (!IsDefaultHost(host))
            {
                // Custom domain-based resolution for unauthenticated requests (login page, public API)
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

    private static readonly string[] SubdomainSuffixes = [".natra.site", ".ivf.clinic"];

    private static string? ExtractSubdomainSlug(string host)
    {
        // Strip port if present (e.g. localhost:5000)
        var colonIdx = host.IndexOf(':');
        if (colonIdx > 0)
            host = host[..colonIdx];

        foreach (var suffix in SubdomainSuffixes)
        {
            if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                && host.Length > suffix.Length)
            {
                var subdomain = host[..^suffix.Length];
                // Only single-level subdomains (no dots), valid slug chars
                if (!subdomain.Contains('.') && subdomain.Length > 0)
                    return subdomain.ToLowerInvariant();
            }
        }
        return null;
    }

    private static bool IsDefaultHost(string host)
    {
        // Strip port if present
        var colonIdx = host.IndexOf(':');
        if (colonIdx > 0)
            host = host[..colonIdx];

        return host is "localhost" or "127.0.0.1"
            || host is "natra.site" or "ivf.clinic";
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
