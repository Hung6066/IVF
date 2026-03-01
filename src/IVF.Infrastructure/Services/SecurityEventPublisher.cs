using System.Net.Http.Json;
using System.Text;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Publishes security events to SIEM systems via:
/// 1. Structured logging (always — captured by any log aggregator)
/// 2. Webhook (optional — for Sentinel, Splunk HEC, custom endpoints)
/// 3. CEF/Syslog format in logs (for traditional SIEM ingestion)
/// </summary>
public class SecurityEventPublisher : ISecurityEventPublisher
{
    private readonly ILogger<SecurityEventPublisher> _logger;
    private readonly IConfiguration _configuration;
    private readonly string? _webhookUrl;
    private readonly bool _webhookEnabled;

    public SecurityEventPublisher(
        ILogger<SecurityEventPublisher> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _webhookUrl = configuration["Siem:WebhookUrl"];
        _webhookEnabled = !string.IsNullOrEmpty(_webhookUrl);
    }

    public async Task PublishAsync(VaultSecurityEvent securityEvent, CancellationToken ct = default)
    {
        // 1. Always emit structured log (works with any log sink — Seq, ELK, CloudWatch, etc.)
        _logger.LogWarning(
            "SECURITY_EVENT: Type={EventType} Severity={Severity} Action={Action} " +
            "User={UserId} IP={IpAddress} Resource={ResourceType}/{ResourceId} " +
            "Outcome={Outcome} Reason={Reason}",
            securityEvent.EventType,
            securityEvent.Severity,
            securityEvent.Action,
            securityEvent.UserId ?? "system",
            securityEvent.IpAddress ?? "unknown",
            securityEvent.ResourceType,
            securityEvent.ResourceId,
            securityEvent.Outcome,
            securityEvent.Reason);

        // 2. CEF format log line (for syslog/SIEM ingestion)
        var cef = FormatCef(securityEvent);
        _logger.LogInformation("CEF:{CefEvent}", cef);

        // 3. Webhook delivery (if configured)
        if (_webhookEnabled)
        {
            await SendWebhookAsync(securityEvent, ct);
        }
    }

    public async Task PublishBatchAsync(IEnumerable<VaultSecurityEvent> events, CancellationToken ct = default)
    {
        foreach (var evt in events)
        {
            await PublishAsync(evt, ct);
        }
    }

    /// <summary>
    /// Format event as CEF (Common Event Format):
    /// CEF:0|IVF|VaultSecurity|1.0|EventType|Action|Severity|extensions
    /// </summary>
    public static string FormatCef(VaultSecurityEvent evt)
    {
        var sb = new StringBuilder();
        sb.Append("0|IVF|VaultSecurity|1.0|");
        sb.Append(EscapeCef(evt.EventType));
        sb.Append('|');
        sb.Append(EscapeCef(evt.Action));
        sb.Append('|');
        sb.Append((int)evt.Severity);
        sb.Append('|');

        // CEF extensions
        if (evt.UserId is not null) sb.Append($"suid={EscapeCef(evt.UserId)} ");
        if (evt.IpAddress is not null) sb.Append($"src={EscapeCef(evt.IpAddress)} ");
        if (evt.ResourceType is not null) sb.Append($"cs1={EscapeCef(evt.ResourceType)} cs1Label=ResourceType ");
        if (evt.ResourceId is not null) sb.Append($"cs2={EscapeCef(evt.ResourceId)} cs2Label=ResourceId ");
        if (evt.Outcome is not null) sb.Append($"outcome={EscapeCef(evt.Outcome)} ");
        if (evt.Reason is not null) sb.Append($"reason={EscapeCef(evt.Reason)} ");
        sb.Append($"rt={evt.Timestamp:o}");

        if (evt.Extensions is not null)
        {
            foreach (var (key, value) in evt.Extensions)
            {
                sb.Append($" {EscapeCef(key)}={EscapeCef(value)}");
            }
        }

        return sb.ToString();
    }

    private static string EscapeCef(string value)
        => value.Replace("\\", "\\\\").Replace("|", "\\|").Replace("=", "\\=");

    private async Task SendWebhookAsync(VaultSecurityEvent evt, CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.PostAsJsonAsync(_webhookUrl, evt, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "SIEM webhook delivery failed: {StatusCode} for event {EventType}",
                    response.StatusCode, evt.EventType);
            }
        }
        catch (Exception ex)
        {
            // Never let SIEM delivery failure affect application flow
            _logger.LogWarning(ex, "SIEM webhook delivery error for event {EventType}", evt.EventType);
        }
    }
}
