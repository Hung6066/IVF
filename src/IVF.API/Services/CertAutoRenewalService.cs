using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;

namespace IVF.API.Services;

/// <summary>
/// Background service that periodically checks for expiring certificates
/// and auto-renews them if AutoRenewEnabled is set.
/// Runs once per hour, renews certs within their RenewBeforeDays window.
/// Also publishes escalating expiry warnings at 30/14/7/1-day thresholds.
/// </summary>
public sealed class CertAutoRenewalService(
    CertificateAuthorityService caService,
    ISecurityEventPublisher securityEvents,
    ILogger<CertAutoRenewalService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(90);
    private static readonly int[] WarningThresholdDays = [30, 14, 7, 1];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);
        logger.LogInformation("Certificate auto-renewal service started (check interval: {Interval})", CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check for expiring certificates and publish warnings
                await PublishExpiryWarningsAsync(stoppingToken);

                var result = await caService.AutoRenewExpiringAsync(stoppingToken);

                if (result.TotalCandidates > 0)
                {
                    logger.LogInformation(
                        "Auto-renewal check: {Candidates} expiring, {Renewed} renewed",
                        result.TotalCandidates, result.RenewedCount);

                    foreach (var r in result.Results.Where(r => !r.Success))
                    {
                        logger.LogWarning("Auto-renewal failed for {CN} ({Purpose}): {Message}",
                            r.CommonName, r.Purpose, r.Message);

                        // Publish security event for failed renewal
                        await securityEvents.PublishAsync(new VaultSecurityEvent
                        {
                            EventType = "certificate.renewal.failed",
                            Severity = SecuritySeverity.High,
                            Source = "CertAutoRenewalService",
                            Action = "cert.renew",
                            ResourceType = "Certificate",
                            ResourceId = r.OldCertId.ToString(),
                            Outcome = "failure",
                            Reason = r.Message,
                            Extensions = new Dictionary<string, string>
                            {
                                ["commonName"] = r.CommonName,
                                ["purpose"] = r.Purpose
                            }
                        });
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Certificate auto-renewal check failed");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task PublishExpiryWarningsAsync(CancellationToken ct)
    {
        try
        {
            var expiring = await caService.GetExpiringCertificatesAsync(ct);

            foreach (var cert in expiring)
            {
                var daysUntilExpiry = (cert.NotAfter - DateTime.UtcNow).TotalDays;

                // Find the matching warning threshold
                var threshold = WarningThresholdDays.FirstOrDefault(d => daysUntilExpiry <= d && daysUntilExpiry > (d == 1 ? 0 : d - 1));
                if (threshold == 0 && daysUntilExpiry > 1) continue;

                var severity = threshold switch
                {
                    <= 1 => SecuritySeverity.Critical,
                    <= 7 => SecuritySeverity.High,
                    <= 14 => SecuritySeverity.Medium,
                    _ => SecuritySeverity.Low
                };

                await securityEvents.PublishAsync(new VaultSecurityEvent
                {
                    EventType = "certificate.expiry.warning",
                    Severity = severity,
                    Source = "CertAutoRenewalService",
                    Action = "cert.expiry.check",
                    ResourceType = "Certificate",
                    ResourceId = cert.Id.ToString(),
                    Outcome = "warning",
                    Reason = $"Certificate expires in {(int)daysUntilExpiry} days",
                    Extensions = new Dictionary<string, string>
                    {
                        ["commonName"] = cert.CommonName,
                        ["purpose"] = cert.Purpose,
                        ["daysUntilExpiry"] = ((int)daysUntilExpiry).ToString(),
                        ["notAfter"] = cert.NotAfter.ToString("O"),
                        ["autoRenewEnabled"] = cert.AutoRenewEnabled.ToString()
                    }
                });

                logger.LogWarning(
                    "Certificate {CN} ({Purpose}) expires in {Days} days (threshold: {Threshold}d, auto-renew: {AutoRenew})",
                    cert.CommonName, cert.Purpose, (int)daysUntilExpiry, threshold, cert.AutoRenewEnabled);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to check certificate expiry warnings");
        }
    }
}
