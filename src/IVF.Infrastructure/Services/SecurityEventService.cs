using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Centralized security event logging inspired by:
/// - AWS CloudTrail: Complete audit trail of all security events
/// - Microsoft Sentinel: Real-time security analytics
/// - Google Chronicle: Security telemetry aggregation
///
/// All security events are persisted to the SecurityEvents table for
/// compliance, threat detection, and forensic analysis.
/// </summary>
public sealed class SecurityEventService : ISecurityEventService
{
    private readonly IvfDbContext _context;
    private readonly ILogger<SecurityEventService> _logger;

    public SecurityEventService(IvfDbContext context, ILogger<SecurityEventService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogEventAsync(SecurityEvent securityEvent, CancellationToken ct = default)
    {
        try
        {
            await _context.SecurityEvents.AddAsync(securityEvent, ct);
            await _context.SaveChangesAsync(ct);

            // Log high-severity events to structured logging (for SIEM integration)
            if (securityEvent.Severity is "High" or "Critical")
            {
                _logger.LogWarning(
                    "Security Event [{Severity}] {EventType}: User={UserId}, IP={IpAddress}, Path={RequestPath}, Details={Details}",
                    securityEvent.Severity,
                    securityEvent.EventType,
                    securityEvent.UserId,
                    securityEvent.IpAddress,
                    securityEvent.RequestPath,
                    securityEvent.Details);
            }
            else
            {
                _logger.LogInformation(
                    "Security Event [{Severity}] {EventType}: User={UserId}, IP={IpAddress}",
                    securityEvent.Severity,
                    securityEvent.EventType,
                    securityEvent.UserId,
                    securityEvent.IpAddress);
            }
        }
        catch (Exception ex)
        {
            // Security event logging should never fail silently, but also shouldn't crash the request
            _logger.LogError(ex, "Failed to log security event {EventType}", securityEvent.EventType);
        }
    }

    public async Task<List<SecurityEvent>> GetRecentEventsAsync(int count = 50, CancellationToken ct = default)
    {
        return await _context.SecurityEvents
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<List<SecurityEvent>> GetEventsByUserAsync(Guid userId, DateTime since, CancellationToken ct = default)
    {
        return await _context.SecurityEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.CreatedAt >= since)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<SecurityEvent>> GetEventsByIpAsync(string ipAddress, DateTime since, CancellationToken ct = default)
    {
        return await _context.SecurityEvents
            .AsNoTracking()
            .Where(e => e.IpAddress == ipAddress && e.CreatedAt >= since)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<SecurityEvent>> GetHighSeverityEventsAsync(DateTime since, CancellationToken ct = default)
    {
        return await _context.SecurityEvents
            .AsNoTracking()
            .Where(e => (e.Severity == "High" || e.Severity == "Critical") && e.CreatedAt >= since)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetFailedLoginCountAsync(string identifier, TimeSpan window, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - window;
        return await _context.SecurityEvents
            .AsNoTracking()
            .CountAsync(e =>
                e.EventType == SecurityEventTypes.LoginFailed &&
                (e.Username == identifier || e.IpAddress == identifier) &&
                e.CreatedAt >= since, ct);
    }
}
