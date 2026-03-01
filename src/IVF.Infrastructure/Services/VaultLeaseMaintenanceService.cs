using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Background service that periodically cleans up expired vault resources:
/// - Revokes expired leases
/// - Revokes expired dynamic credentials and drops their DB roles
/// - Marks expired tokens as revoked
/// Runs every 5 minutes. All revocations are audit-logged.
/// </summary>
public class VaultLeaseMaintenanceService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VaultLeaseMaintenanceService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public VaultLeaseMaintenanceService(
        IServiceScopeFactory scopeFactory,
        ILogger<VaultLeaseMaintenanceService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app startup
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        _logger.LogInformation("Vault lease maintenance service started (interval: {Interval})", _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMaintenanceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during vault lease maintenance");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task RunMaintenanceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IVaultRepository>();

        var expiredLeases = await RevokeExpiredLeasesAsync(repo, ct);
        var expiredCreds = await RevokeExpiredCredentialsAsync(scope.ServiceProvider, repo, ct);
        var expiredTokens = await RevokeExpiredTokensAsync(repo, ct);

        // Execute pending secret rotations
        var rotationResult = await ExecutePendingRotationsAsync(scope.ServiceProvider, ct);

        // Update gauge metrics
        await UpdateGaugeMetricsAsync(scope.ServiceProvider, repo, ct);

        if (expiredLeases + expiredCreds + expiredTokens > 0)
        {
            _logger.LogInformation(
                "Vault maintenance completed: {Leases} leases, {Creds} credentials, {Tokens} tokens revoked",
                expiredLeases, expiredCreds, expiredTokens);
        }
    }

    private async Task<int> RevokeExpiredLeasesAsync(IVaultRepository repo, CancellationToken ct)
    {
        var leases = await repo.GetLeasesAsync(includeExpired: true, ct);
        var expired = leases.Where(l => l.IsExpired && !l.Revoked).ToList();
        var revoked = 0;

        foreach (var lease in expired)
        {
            try
            {
                await repo.RevokeLeaseAsync(lease.LeaseId, ct);
                await repo.AddAuditLogAsync(VaultAuditLog.Create(
                    "lease.auto-revoke", "Lease", lease.LeaseId,
                    details: $"{{\"reason\":\"expired\",\"expiredAt\":\"{lease.ExpiresAt:O}\"}}"));
                revoked++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-revoke lease {LeaseId}", lease.LeaseId);
            }
        }

        return revoked;
    }

    private async Task<int> RevokeExpiredCredentialsAsync(
        IServiceProvider sp, IVaultRepository repo, CancellationToken ct)
    {
        var credProvider = sp.GetRequiredService<IDynamicCredentialProvider>();
        var revokedCount = await credProvider.RevokeExpiredCredentialsAsync(ct);

        // Audit log each revocation
        if (revokedCount > 0)
        {
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "dynamic.auto-revoke", "DynamicCredential", null,
                details: $"{{\"count\":{revokedCount},\"reason\":\"expired\"}}"));
        }

        return revokedCount;
    }

    private async Task<int> RevokeExpiredTokensAsync(IVaultRepository repo, CancellationToken ct)
    {
        var tokens = await repo.GetTokensAsync(includeRevoked: false, ct);
        var expired = tokens.Where(t => (t.IsExpired || t.IsExhausted) && !t.Revoked).ToList();
        var revoked = 0;

        foreach (var token in expired)
        {
            try
            {
                await repo.RevokeTokenAsync(token.Id, ct);
                await repo.AddAuditLogAsync(VaultAuditLog.Create(
                    "token.auto-revoke", "Token", token.Id.ToString(),
                    details: $"{{\"reason\":\"{(token.IsExpired ? "expired" : "exhausted")}\",\"accessor\":\"{token.Accessor}\"}}"));
                revoked++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-revoke token {Accessor}", token.Accessor);
            }
        }

        return revoked;
    }

    private async Task<int> ExecutePendingRotationsAsync(IServiceProvider sp, CancellationToken ct)
    {
        try
        {
            var rotationService = sp.GetRequiredService<ISecretRotationService>();
            var result = await rotationService.ExecutePendingRotationsAsync(ct);
            return result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to execute pending secret rotations");
            return 0;
        }
    }

    private async Task UpdateGaugeMetricsAsync(IServiceProvider sp, IVaultRepository repo, CancellationToken ct)
    {
        try
        {
            var metrics = sp.GetRequiredService<VaultMetrics>();

            var leases = await repo.GetLeasesAsync(includeExpired: false, ct);
            metrics.SetActiveLeases(leases.Count(l => !l.Revoked && !l.IsExpired));

            var secrets = await repo.ListSecretsAsync(null, ct);
            metrics.SetSecretsCount(secrets.Count);

            var schedules = await repo.GetRotationSchedulesAsync(activeOnly: true, ct);
            metrics.SetActiveRotationSchedules(schedules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to update vault gauge metrics");
        }
    }
}
