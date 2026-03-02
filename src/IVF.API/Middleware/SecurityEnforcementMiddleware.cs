using System.Net;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Middleware;

/// <summary>
/// Enforces IP whitelist and geo-blocking rules from the database.
/// Runs early in the pipeline — before authentication — to block
/// disallowed IPs and geo-regions at the network boundary.
/// </summary>
public class SecurityEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityEnforcementMiddleware> _logger;

    // Paths that bypass enforcement (health checks, static files)
    private static readonly HashSet<string> ExemptPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/healthz",
        "/swagger",
    };

    public SecurityEnforcementMiddleware(RequestDelegate next, ILogger<SecurityEnforcementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip for exempt paths
        if (ExemptPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIp(context);
        if (string.IsNullOrEmpty(clientIp))
        {
            await _next(context);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        // ── 1. IP Whitelist check ──
        // If the whitelist has active entries, only whitelisted IPs may proceed.
        var hasWhitelistEntries = await db.IpWhitelistEntries
            .AnyAsync(e => e.IsActive && !e.IsDeleted);

        if (hasWhitelistEntries)
        {
            var isWhitelisted = await IsIpWhitelisted(db, clientIp);
            if (!isWhitelisted)
            {
                _logger.LogWarning("Blocked request from non-whitelisted IP {Ip} to {Path}", clientIp, path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Access denied",
                    code = "IP_NOT_WHITELISTED",
                    message = "Your IP address is not in the allowed list"
                });
                return;
            }
        }

        // ── 2. Geo-blocking check ──
        // Only applies when the request has country info (set by upstream middleware/headers)
        var country = context.Request.Headers["X-Country-Code"].FirstOrDefault()
            ?? context.Items["GeoCountry"]?.ToString();

        if (!string.IsNullOrEmpty(country))
        {
            var isGeoBlocked = await db.GeoBlockRules
                .AnyAsync(r => r.CountryCode == country && r.IsBlocked && r.IsEnabled && !r.IsDeleted);

            if (isGeoBlocked)
            {
                _logger.LogWarning("Blocked request from geo-blocked country {Country} IP {Ip} to {Path}", country, clientIp, path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Access denied",
                    code = "GEO_BLOCKED",
                    message = $"Access from {country} is not permitted"
                });
                return;
            }
        }

        await _next(context);
    }

    private static async Task<bool> IsIpWhitelisted(IvfDbContext db, string clientIp)
    {
        var entries = await db.IpWhitelistEntries
            .Where(e => e.IsActive && !e.IsDeleted)
            .ToListAsync();

        foreach (var entry in entries)
        {
            // Check expiry
            if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
                continue;

            // Exact match
            if (string.Equals(entry.IpAddress, clientIp, StringComparison.OrdinalIgnoreCase))
                return true;

            // CIDR range match
            if (!string.IsNullOrEmpty(entry.CidrRange))
            {
                var cidrNotation = entry.IpAddress + entry.CidrRange;
                if (IsIpInCidr(clientIp, cidrNotation))
                    return true;
            }
        }

        return false;
    }

    private static bool IsIpInCidr(string ipAddress, string cidrNotation)
    {
        try
        {
            var parts = cidrNotation.Split('/');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var prefixLength))
                return false;

            if (!IPAddress.TryParse(parts[0], out var networkAddress) ||
                !IPAddress.TryParse(ipAddress, out var clientAddress))
                return false;

            var networkBytes = networkAddress.GetAddressBytes();
            var clientBytes = clientAddress.GetAddressBytes();

            if (networkBytes.Length != clientBytes.Length)
                return false;

            var totalBits = networkBytes.Length * 8;
            if (prefixLength > totalBits)
                return false;

            for (int i = 0; i < prefixLength; i++)
            {
                var byteIndex = i / 8;
                var bitIndex = 7 - (i % 8);
                var mask = 1 << bitIndex;

                if ((networkBytes[byteIndex] & mask) != (clientBytes[byteIndex] & mask))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetClientIp(HttpContext context)
    {
        // Check forwarded headers first (behind proxy/load balancer)
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            // Take the first IP (original client)
            var firstIp = forwarded.Split(',')[0].Trim();
            if (IPAddress.TryParse(firstIp, out _))
                return firstIp;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}

/// <summary>
/// Extension method for registering the Security Enforcement middleware.
/// </summary>
public static class SecurityEnforcementMiddlewareExtensions
{
    /// <summary>
    /// Adds IP whitelist and geo-blocking enforcement middleware.
    /// Place AFTER CORS but BEFORE authentication middleware.
    /// </summary>
    public static IApplicationBuilder UseSecurityEnforcement(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityEnforcementMiddleware>();
    }
}
