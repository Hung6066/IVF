using IVF.Application.Common.Interfaces;

namespace IVF.API.Services;

/// <summary>
/// Background service that runs automated compliance scans on a schedule.
/// Publishes security events for failed controls and tracks compliance trends.
/// Vanta/Drata-style continuous compliance monitoring.
/// </summary>
public sealed class ComplianceScanSchedulerService(
    IServiceScopeFactory scopeFactory,
    ComplianceAuditorService auditor,
    ILogger<ComplianceScanSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);
        logger.LogInformation("Compliance scan scheduler started (interval: {Interval})", ScanInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

                await using var lockHandle = await lockService.TryAcquireAsync(
                    "lock:compliance-scan", TimeSpan.FromMinutes(10), stoppingToken);

                if (lockHandle is null)
                {
                    logger.LogDebug("Another replica is running compliance scan — skipping");
                    await Task.Delay(ScanInterval, stoppingToken);
                    continue;
                }

                var scan = await auditor.RunScanAsync();

                logger.LogInformation(
                    "Compliance scan completed: Score={Score}%, Passed={Passed}/{Total}, Failed={Failed}",
                    scan.OverallScore, scan.PassedControls, scan.TotalControls, scan.FailedControls);

                // Publish security events for critical/high failures
                if (scan.FailedControls > 0)
                {
                    var securityEvents = scope.ServiceProvider.GetRequiredService<ISecurityEventPublisher>();
                    var criticalFailures = scan.Frameworks
                        .SelectMany(f => f.Controls)
                        .Where(c => c.Status == "Failed" && c.Severity is "Critical" or "High")
                        .ToList();

                    foreach (var failure in criticalFailures)
                    {
                        await securityEvents.PublishAsync(new VaultSecurityEvent
                        {
                            EventType = "compliance.control.failed",
                            Severity = failure.Severity == "Critical"
                                ? IVF.Domain.Enums.SecuritySeverity.Critical
                                : IVF.Domain.Enums.SecuritySeverity.High,
                            Source = "ComplianceScanScheduler",
                            Action = "compliance.scan",
                            ResourceType = "ComplianceControl",
                            ResourceId = failure.Id,
                            Outcome = "failure",
                            Reason = failure.Finding ?? failure.Name,
                            Extensions = new Dictionary<string, string>
                            {
                                ["controlName"] = failure.Name,
                                ["category"] = failure.Category,
                                ["remediation"] = failure.Remediation ?? "",
                                ["overallScore"] = scan.OverallScore.ToString("F1")
                            }
                        }, stoppingToken);
                    }

                    if (criticalFailures.Count > 0)
                    {
                        logger.LogWarning(
                            "Compliance scan found {Count} critical/high failures requiring attention",
                            criticalFailures.Count);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Compliance scan failed");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }
    }
}
