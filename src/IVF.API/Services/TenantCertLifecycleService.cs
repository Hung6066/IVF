using IVF.Application.Common.Interfaces;

namespace IVF.API.Services;

/// <summary>
/// Background service that auto-renews expiring user signing certificates
/// issued by tenant Sub-CAs. Runs alongside CertAutoRenewalService which
/// handles infrastructure/mTLS certs — this service handles tenant-scoped
/// PDF signing certs specifically.
///
/// Uses distributed lock to ensure only one replica executes at a time.
/// </summary>
public sealed class TenantCertLifecycleService(
    TenantCertificateService tenantCaService,
    IDistributedLockService lockService,
    ILogger<TenantCertLifecycleService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(2);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);
        logger.LogInformation("Tenant cert lifecycle service started (interval: {Interval})", CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var lockHandle = await lockService.TryAcquireAsync(
                    "lock:tenant-cert-lifecycle", TimeSpan.FromMinutes(15), stoppingToken);

                if (lockHandle is null)
                {
                    logger.LogDebug("Another replica is running tenant cert lifecycle — skipping");
                    await Task.Delay(CheckInterval, stoppingToken);
                    continue;
                }

                var renewed = await tenantCaService.AutoRenewTenantUserCertsAsync(stoppingToken);
                if (renewed > 0)
                    logger.LogInformation("Tenant cert lifecycle: renewed {Count} user signing certs", renewed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in tenant cert lifecycle service");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }
}
