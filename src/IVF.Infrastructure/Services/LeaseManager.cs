using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Manages vault leases for secrets. Provides programmatic lease creation,
/// renewal, and revocation. Leased secrets are only accessible while the lease is active.
/// </summary>
public class LeaseManager : ILeaseManager
{
    private readonly IVaultRepository _repo;
    private readonly IVaultSecretService _secretService;
    private readonly ILogger<LeaseManager> _logger;
    private readonly VaultMetrics _metrics;

    public LeaseManager(
        IVaultRepository repo,
        IVaultSecretService secretService,
        ILogger<LeaseManager> logger,
        VaultMetrics metrics)
    {
        _repo = repo;
        _secretService = secretService;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<LeaseInfo> CreateLeaseAsync(
        string secretPath, int ttlSeconds, bool renewable = true, CancellationToken ct = default)
    {
        var secret = await _secretService.GetSecretAsync(secretPath, ct: ct)
            ?? throw new InvalidOperationException($"Secret '{secretPath}' not found");

        var lease = VaultLease.Create(secret.Id, ttlSeconds, renewable);
        await _repo.AddLeaseAsync(lease, ct);

        await _repo.AddAuditLogAsync(VaultAuditLog.Create(
            "lease.create", "Lease", lease.LeaseId,
            details: $"{{\"secretPath\":\"{secretPath}\",\"ttl\":{ttlSeconds}}}"));

        _logger.LogInformation("Lease created: {LeaseId} for secret {Path}, TTL={Ttl}s",
            lease.LeaseId, secretPath, ttlSeconds);
        _metrics.RecordSecretOperation("lease_create", secretPath);

        return new LeaseInfo(
            lease.LeaseId, secret.Id, secretPath,
            ttlSeconds, renewable, lease.ExpiresAt);
    }

    public async Task<LeaseInfo> RenewLeaseAsync(
        string leaseId, int incrementSeconds, CancellationToken ct = default)
    {
        var lease = await _repo.GetLeaseByIdAsync(leaseId, ct)
            ?? throw new InvalidOperationException($"Lease '{leaseId}' not found");

        if (!lease.Renewable)
            throw new InvalidOperationException($"Lease '{leaseId}' is not renewable");

        if (lease.Revoked)
            throw new InvalidOperationException($"Lease '{leaseId}' is already revoked");

        lease.Renew(incrementSeconds);
        await _repo.UpdateLeaseAsync(lease, ct);

        await _repo.AddAuditLogAsync(VaultAuditLog.Create(
            "lease.renew", "Lease", leaseId,
            details: $"{{\"newExpiry\":\"{lease.ExpiresAt:O}\",\"increment\":{incrementSeconds}}}"));

        _logger.LogInformation("Lease renewed: {LeaseId}, new expiry {ExpiresAt}", leaseId, lease.ExpiresAt);
        _metrics.RecordSecretOperation("lease_renew", leaseId);

        return new LeaseInfo(
            lease.LeaseId, lease.SecretId, lease.Secret?.Path,
            lease.Ttl, lease.Renewable, lease.ExpiresAt);
    }

    public async Task RevokeLeaseAsync(string leaseId, CancellationToken ct = default)
    {
        await _repo.RevokeLeaseAsync(leaseId, ct);

        await _repo.AddAuditLogAsync(VaultAuditLog.Create(
            "lease.revoke", "Lease", leaseId));

        _metrics.RecordSecretOperation("lease_revoke", leaseId);
        _logger.LogInformation("Lease revoked: {LeaseId}", leaseId);
    }

    public async Task<LeasedSecretResult?> GetLeasedSecretAsync(
        string leaseId, CancellationToken ct = default)
    {
        var lease = await _repo.GetLeaseByIdAsync(leaseId, ct);
        if (lease is null || lease.Revoked || lease.IsExpired)
            return null;

        var secret = await _repo.GetSecretByIdAsync(lease.SecretId, ct);
        if (secret is null)
            return null;

        // Decrypt the secret value via the secret service
        var decrypted = await _secretService.GetSecretAsync(secret.Path, ct: ct);
        if (decrypted is null)
            return null;

        var remaining = (int)(lease.ExpiresAt - DateTime.UtcNow).TotalSeconds;
        return new LeasedSecretResult(
            leaseId, secret.Path, decrypted.Value, lease.ExpiresAt,
            Math.Max(0, remaining));
    }

    public async Task<IReadOnlyList<LeaseInfo>> GetActiveLeasesAsync(CancellationToken ct = default)
    {
        var leases = await _repo.GetLeasesAsync(includeExpired: false, ct);
        return leases
            .Where(l => !l.Revoked && !l.IsExpired)
            .Select(l => new LeaseInfo(
                l.LeaseId, l.SecretId, l.Secret?.Path,
                l.Ttl, l.Renewable, l.ExpiresAt))
            .ToList()
            .AsReadOnly();
    }
}
