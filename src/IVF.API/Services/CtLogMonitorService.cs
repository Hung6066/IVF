using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IVF.Application.Common.Interfaces;

namespace IVF.API.Services;

/// <summary>
/// Background service that monitors Certificate Transparency (CT) logs for the configured domain.
/// Detects unauthorized certificate issuance (rogue CAs, domain hijacking, phishing certs).
/// Uses the crt.sh public CT log aggregator API.
/// </summary>
public sealed class CtLogMonitorService(
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CtLogMonitorService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);
    private DateTime _lastCheckedAt = DateTime.UtcNow.AddDays(-1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var domain = configuration.GetValue<string>("CtLogMonitor:Domain") ?? "natra.site";
        var enabled = configuration.GetValue("CtLogMonitor:Enabled", true);

        if (!enabled)
        {
            logger.LogInformation("CT log monitoring is disabled");
            return;
        }

        await Task.Delay(InitialDelay, stoppingToken);
        logger.LogInformation("CT log monitor started for domain: {Domain} (interval: {Interval})", domain, CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

                await using var lockHandle = await lockService.TryAcquireAsync(
                    "lock:ct-log-monitor", TimeSpan.FromMinutes(5), stoppingToken);

                if (lockHandle is null)
                {
                    await Task.Delay(CheckInterval, stoppingToken);
                    continue;
                }

                await CheckCtLogsAsync(domain, scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CT log check failed for domain");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckCtLogsAsync(string domain, IServiceProvider sp, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IVF-CT-Monitor/1.0");

        // Query crt.sh for certificates issued for our domain
        var url = $"https://crt.sh/?q=%.{Uri.EscapeDataString(domain)}&output=json";

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to reach crt.sh — CT log check skipped");
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("crt.sh returned {StatusCode} — CT log check skipped", response.StatusCode);
            return;
        }

        var certs = await response.Content.ReadFromJsonAsync<List<CtLogEntry>>(cancellationToken: ct);

        if (certs is null || certs.Count == 0)
        {
            logger.LogDebug("No CT log entries found for {Domain}", domain);
            _lastCheckedAt = DateTime.UtcNow;
            return;
        }

        // Filter to certs issued since last check
        var newCerts = certs
            .Where(c => c.EntryTimestamp > _lastCheckedAt)
            .OrderByDescending(c => c.EntryTimestamp)
            .ToList();

        if (newCerts.Count == 0)
        {
            logger.LogDebug("No new CT log entries since {LastChecked}", _lastCheckedAt);
            _lastCheckedAt = DateTime.UtcNow;
            return;
        }

        logger.LogInformation("Found {Count} new certificate(s) in CT logs for {Domain}", newCerts.Count, domain);

        // Known trusted issuers (Let's Encrypt, Cloudflare)
        var trustedIssuers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Let's Encrypt",
            "R3", "R10", "R11", "E5", "E6", // Let's Encrypt intermediates
            "Cloudflare Inc ECC CA-3",
            "Google Trust Services",
            "DigiCert",
        };

        var securityEvents = sp.GetRequiredService<ISecurityEventPublisher>();

        foreach (var cert in newCerts)
        {
            var issuerOrg = cert.IssuerName ?? "Unknown";
            var isTrusted = trustedIssuers.Any(t => issuerOrg.Contains(t, StringComparison.OrdinalIgnoreCase));

            if (!isTrusted)
            {
                logger.LogWarning(
                    "UNTRUSTED certificate detected in CT logs! CN={CommonName}, Issuer={Issuer}, NotBefore={NotBefore}",
                    cert.CommonName, issuerOrg, cert.NotBefore);

                await securityEvents.PublishAsync(new VaultSecurityEvent
                {
                    EventType = "ct.certificate.untrusted",
                    Severity = IVF.Domain.Enums.SecuritySeverity.Critical,
                    Source = "CtLogMonitorService",
                    Action = "ct.log.check",
                    ResourceType = "Certificate",
                    ResourceId = cert.Id.ToString(),
                    Outcome = "alert",
                    Reason = $"Untrusted CA issued certificate for {cert.CommonName}: {issuerOrg}",
                    Extensions = new Dictionary<string, string>
                    {
                        ["commonName"] = cert.CommonName ?? "",
                        ["issuer"] = issuerOrg,
                        ["notBefore"] = cert.NotBefore?.ToString("O") ?? "",
                        ["notAfter"] = cert.NotAfter?.ToString("O") ?? "",
                        ["serialNumber"] = cert.SerialNumber ?? ""
                    }
                }, ct);
            }
            else
            {
                logger.LogDebug("Trusted certificate in CT logs: CN={CommonName}, Issuer={Issuer}",
                    cert.CommonName, issuerOrg);
            }
        }

        _lastCheckedAt = DateTime.UtcNow;
    }

    private sealed class CtLogEntry
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("issuer_name")]
        public string? IssuerName { get; set; }

        [JsonPropertyName("common_name")]
        public string? CommonName { get; set; }

        [JsonPropertyName("name_value")]
        public string? NameValue { get; set; }

        [JsonPropertyName("serial_number")]
        public string? SerialNumber { get; set; }

        [JsonPropertyName("not_before")]
        public DateTime? NotBefore { get; set; }

        [JsonPropertyName("not_after")]
        public DateTime? NotAfter { get; set; }

        [JsonPropertyName("entry_timestamp")]
        public DateTime EntryTimestamp { get; set; }
    }
}
