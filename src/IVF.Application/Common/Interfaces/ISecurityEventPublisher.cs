using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Publishes security events to external SIEM systems (Syslog/CEF, webhooks).
/// Supports Microsoft Sentinel, Splunk, AWS CloudTrail integration patterns.
/// </summary>
public interface ISecurityEventPublisher
{
    Task PublishAsync(VaultSecurityEvent securityEvent, CancellationToken ct = default);
    Task PublishBatchAsync(IEnumerable<VaultSecurityEvent> events, CancellationToken ct = default);
}

/// <summary>
/// A structured security event following CEF (Common Event Format) conventions.
/// </summary>
public record VaultSecurityEvent
{
    public required string EventType { get; init; }
    public required SecuritySeverity Severity { get; init; }
    public required string Source { get; init; }
    public required string Action { get; init; }
    public string? UserId { get; init; }
    public string? IpAddress { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public string? Outcome { get; init; }
    public string? Reason { get; init; }
    public Dictionary<string, string>? Extensions { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
