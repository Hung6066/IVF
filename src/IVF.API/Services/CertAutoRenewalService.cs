namespace IVF.API.Services;

/// <summary>
/// Background service that periodically checks for expiring certificates
/// and auto-renews them if AutoRenewEnabled is set.
/// Runs once per hour, renews certs within their RenewBeforeDays window.
/// </summary>
public sealed class CertAutoRenewalService(
    CertificateAuthorityService caService,
    ILogger<CertAutoRenewalService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(90);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);
        logger.LogInformation("Certificate auto-renewal service started (check interval: {Interval})", CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
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
}
