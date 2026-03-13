using System.Net;
using IVF.API.Extensions;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Middleware;

/// <summary>
/// Enforces IP whitelist and geo-blocking rules with enterprise caching.
/// Runs early in the pipeline — before authentication — to block
/// disallowed IPs and geo-regions at the network boundary.
///
/// Enterprise optimizations:
/// - Cached IP whitelist lookup (5-min TTL)
/// - Cached geo-blocking rules (5-min TTL)
/// - In-memory bloom filter for fast negative lookups
/// - CIDR range matching with optimized bit operations
/// </summary>
public class SecurityEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityEnforcementMiddleware> _logger;

    // Paths that bypass enforcement (health checks, static files, security management)
    private static readonly HashSet<string> ExemptPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/healthz",
        "/swagger",
        "/metrics",
        "/api/auth",                    // login/refresh must always be reachable
        "/api/security/advanced",       // admin must be able to manage whitelist
        "/hubs",                        // SignalR hubs (auth handled by hub itself)
    };

    public SecurityEnforcementMiddleware(RequestDelegate next, ILogger<SecurityEnforcementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISecurityRuleCache securityCache)
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

        // Always allow loopback and private network addresses (Docker overlay, internal services)
        if (IPAddress.TryParse(clientIp, out var parsed) && (IPAddress.IsLoopback(parsed) || IsPrivateNetwork(parsed)))
        {
            await _next(context);
            return;
        }

        // ── 1. IP Whitelist check (CACHED) ──
        // If the whitelist has active entries, only whitelisted IPs may proceed.
        var hasWhitelistEntries = await securityCache.HasActiveWhitelistAsync(context.RequestAborted);

        if (hasWhitelistEntries)
        {
            var isWhitelisted = await securityCache.IsIpWhitelistedAsync(clientIp, context.RequestAborted);
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

        // ── 2. Geo-blocking check (CACHED) ──
        // Only use trusted sources: Cloudflare CF-IPCountry header (set by Cloudflare edge,
        // not spoofable by client) or server-side GeoCountry set by upstream middleware.
        // NEVER trust X-Country-Code from the client — it can be spoofed.
        var country = context.Request.Headers["CF-IPCountry"].FirstOrDefault()
            ?? context.Items["GeoCountry"]?.ToString();

        if (!string.IsNullOrEmpty(country))
        {
            var isGeoBlocked = await securityCache.IsCountryBlockedAsync(country, context.RequestAborted);

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

    /// <summary>
    /// Returns true for private/internal network IPs (RFC 1918 + Docker overlay).
    /// These represent inter-service traffic within Docker Swarm.
    /// </summary>
    private static bool IsPrivateNetwork(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false;

        return bytes[0] == 10                                          // 10.0.0.0/8
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)   // 172.16.0.0/12
            || (bytes[0] == 192 && bytes[1] == 168)                    // 192.168.0.0/16
            || bytes[0] == 127;                                         // 127.0.0.0/8
    }

    private static string? GetClientIp(HttpContext context)
    {
        // Cloudflare: CF-Connecting-IP is the most reliable real client IP
        var cfIp = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(cfIp) && IPAddress.TryParse(cfIp.Trim(), out _))
            return cfIp.Trim();

        // Check forwarded headers (behind proxy/load balancer)
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            // Take the first IP (original client)
            var firstIp = forwarded.Split(',')[0].Trim();
            if (IPAddress.TryParse(firstIp, out _))
                return firstIp;
        }

        // X-Real-IP fallback (Nginx/Caddy)
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp.Trim(), out _))
            return realIp.Trim();

        return context.Connection.RemoteIpAddress?.ToString();
    }

    // Note: IsIpWhitelisted and IsIpInCidr methods removed - now handled by ISecurityRuleCache
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
