using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class VaultRepository : IVaultRepository
{
    private readonly IvfDbContext _db;

    public VaultRepository(IvfDbContext db) => _db = db;

    // ─── Secrets ──────────────────────────────────────────

    public async Task<VaultSecret?> GetSecretAsync(string path, int? version = null, CancellationToken ct = default)
    {
        var query = _db.VaultSecrets.Where(s => s.Path == path && s.DeletedAt == null);
        if (version.HasValue)
            query = query.Where(s => s.Version == version.Value);
        else
            query = query.OrderByDescending(s => s.Version);

        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<VaultSecret?> GetSecretByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.VaultSecrets.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<List<VaultSecret>> ListSecretsAsync(string? pathPrefix = null, CancellationToken ct = default)
    {
        var query = _db.VaultSecrets.Where(s => s.DeletedAt == null);

        if (!string.IsNullOrEmpty(pathPrefix))
        {
            var prefix = pathPrefix.TrimEnd('/');
            query = query.Where(s => s.Path.StartsWith(prefix + "/") || s.Path.StartsWith(prefix + "-"));
        }

        // Get only latest version per path using a subquery approach (EF Core compatible)
        var latestVersions = _db.VaultSecrets
            .Where(s => s.DeletedAt == null)
            .GroupBy(s => s.Path)
            .Select(g => new { Path = g.Key, MaxVersion = g.Max(s => s.Version) });

        var result = from s in query
                     join lv in latestVersions on new { s.Path, s.Version } equals new { lv.Path, Version = lv.MaxVersion }
                     orderby s.Path
                     select s;

        return await result.ToListAsync(ct);
    }

    public async Task<int> GetLatestVersionAsync(string path, CancellationToken ct = default)
    {
        var maxVersion = await _db.VaultSecrets
            .Where(s => s.Path == path)
            .MaxAsync(s => (int?)s.Version, ct);
        return maxVersion ?? 0;
    }

    public async Task AddSecretAsync(VaultSecret secret, CancellationToken ct = default)
    {
        await _db.VaultSecrets.AddAsync(secret, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateSecretAsync(VaultSecret secret, CancellationToken ct = default)
    {
        _db.VaultSecrets.Update(secret);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteSecretAsync(string path, CancellationToken ct = default)
    {
        var secrets = await _db.VaultSecrets.Where(s => s.Path == path).ToListAsync(ct);
        foreach (var s in secrets)
            s.SoftDelete();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<VaultSecret>> GetSecretVersionsAsync(string path, CancellationToken ct = default)
        => await _db.VaultSecrets
            .Where(s => s.Path == path)
            .OrderByDescending(s => s.Version)
            .ToListAsync(ct);

    // ─── Policies ─────────────────────────────────────────

    public async Task<List<VaultPolicy>> GetPoliciesAsync(CancellationToken ct = default)
        => await _db.VaultPolicies.OrderBy(p => p.Name).ToListAsync(ct);

    public async Task<VaultPolicy?> GetPolicyByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.VaultPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<VaultPolicy?> GetPolicyByNameAsync(string name, CancellationToken ct = default)
        => await _db.VaultPolicies.FirstOrDefaultAsync(p => p.Name == name, ct);

    public async Task AddPolicyAsync(VaultPolicy policy, CancellationToken ct = default)
    {
        await _db.VaultPolicies.AddAsync(policy, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdatePolicyAsync(VaultPolicy policy, CancellationToken ct = default)
    {
        _db.VaultPolicies.Update(policy);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeletePolicyAsync(Guid id, CancellationToken ct = default)
    {
        var policy = await _db.VaultPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy is not null)
        {
            policy.MarkAsDeleted();
            await _db.SaveChangesAsync(ct);
        }
    }

    // ─── User Policies ────────────────────────────────────

    public async Task<List<VaultUserPolicy>> GetUserPoliciesAsync(CancellationToken ct = default)
        => await _db.VaultUserPolicies
            .Include(up => up.User)
            .Include(up => up.Policy)
            .OrderBy(up => up.GrantedAt)
            .ToListAsync(ct);

    public async Task<List<VaultUserPolicy>> GetUserPoliciesByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _db.VaultUserPolicies
            .Include(up => up.Policy)
            .Where(up => up.UserId == userId)
            .ToListAsync(ct);

    public async Task AddUserPolicyAsync(VaultUserPolicy userPolicy, CancellationToken ct = default)
    {
        await _db.VaultUserPolicies.AddAsync(userPolicy, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveUserPolicyAsync(Guid id, CancellationToken ct = default)
    {
        var up = await _db.VaultUserPolicies.FirstOrDefaultAsync(up => up.Id == id, ct);
        if (up is not null)
        {
            up.MarkAsDeleted();
            await _db.SaveChangesAsync(ct);
        }
    }

    // ─── Leases ───────────────────────────────────────────

    public async Task<List<VaultLease>> GetLeasesAsync(bool includeExpired = false, CancellationToken ct = default)
    {
        var query = _db.VaultLeases.Include(l => l.Secret).AsQueryable();
        if (!includeExpired)
            query = query.Where(l => !l.Revoked && l.ExpiresAt > DateTime.UtcNow);
        return await query.OrderBy(l => l.ExpiresAt).ToListAsync(ct);
    }

    public async Task<VaultLease?> GetLeaseByIdAsync(string leaseId, CancellationToken ct = default)
        => await _db.VaultLeases
            .Include(l => l.Secret)
            .FirstOrDefaultAsync(l => l.LeaseId == leaseId, ct);

    public async Task AddLeaseAsync(VaultLease lease, CancellationToken ct = default)
    {
        await _db.VaultLeases.AddAsync(lease, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateLeaseAsync(VaultLease lease, CancellationToken ct = default)
    {
        _db.VaultLeases.Update(lease);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeLeaseAsync(string leaseId, CancellationToken ct = default)
    {
        var lease = await _db.VaultLeases.FirstOrDefaultAsync(l => l.LeaseId == leaseId, ct);
        if (lease is not null)
        {
            lease.Revoke();
            await _db.SaveChangesAsync(ct);
        }
    }

    // ─── Dynamic Credentials ──────────────────────────────

    public async Task<List<VaultDynamicCredential>> GetDynamicCredentialsAsync(bool includeRevoked = false, CancellationToken ct = default)
    {
        var query = _db.VaultDynamicCredentials.AsQueryable();
        if (!includeRevoked)
            query = query.Where(d => !d.Revoked);
        return await query.OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
    }

    public async Task<VaultDynamicCredential?> GetDynamicCredentialByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.VaultDynamicCredentials.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task AddDynamicCredentialAsync(VaultDynamicCredential credential, CancellationToken ct = default)
    {
        await _db.VaultDynamicCredentials.AddAsync(credential, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeDynamicCredentialAsync(Guid id, CancellationToken ct = default)
    {
        var cred = await _db.VaultDynamicCredentials.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (cred is not null)
        {
            cred.Revoke();
            await _db.SaveChangesAsync(ct);
        }
    }

    // ─── Tokens ───────────────────────────────────────────

    public async Task<List<VaultToken>> GetTokensAsync(bool includeRevoked = false, CancellationToken ct = default)
    {
        var query = _db.VaultTokens.AsQueryable();
        if (!includeRevoked)
            query = query.Where(t => !t.Revoked);
        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
    }

    public async Task<VaultToken?> GetTokenByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.VaultTokens.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<VaultToken?> GetTokenByHashAsync(string tokenHash, CancellationToken ct = default)
        => await _db.VaultTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddTokenAsync(VaultToken token, CancellationToken ct = default)
    {
        await _db.VaultTokens.AddAsync(token, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateTokenAsync(VaultToken token, CancellationToken ct = default)
    {
        _db.VaultTokens.Update(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeTokenAsync(Guid id, CancellationToken ct = default)
    {
        var token = await _db.VaultTokens.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (token is not null)
        {
            token.Revoke();
            await _db.SaveChangesAsync(ct);
        }
    }

    // ─── Auto-Unseal ─────────────────────────────────────

    public async Task<VaultAutoUnseal?> GetAutoUnsealConfigAsync(CancellationToken ct = default)
        => await _db.VaultAutoUnseals.OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(ct);

    public async Task SaveAutoUnsealConfigAsync(VaultAutoUnseal config, CancellationToken ct = default)
    {
        var existing = await GetAutoUnsealConfigAsync(ct);
        if (existing is not null)
        {
            existing.UpdateWrappedKey(config.WrappedKey, config.Iv, config.KeyVersion);
            _db.VaultAutoUnseals.Update(existing);
        }
        else
        {
            await _db.VaultAutoUnseals.AddAsync(config, ct);
        }
        await _db.SaveChangesAsync(ct);
    }

    // ─── Settings ─────────────────────────────────────────

    public async Task<VaultSetting?> GetSettingAsync(string key, CancellationToken ct = default)
        => await _db.VaultSettings.FirstOrDefaultAsync(s => s.Key == key, ct);

    public async Task<List<VaultSetting>> GetAllSettingsAsync(CancellationToken ct = default)
        => await _db.VaultSettings.OrderBy(s => s.Key).ToListAsync(ct);

    public async Task SaveSettingAsync(string key, string valueJson, CancellationToken ct = default)
    {
        var existing = await _db.VaultSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing is not null)
        {
            existing.Update(valueJson);
            _db.VaultSettings.Update(existing);
        }
        else
        {
            await _db.VaultSettings.AddAsync(VaultSetting.Create(key, valueJson), ct);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteSettingAsync(string key, CancellationToken ct = default)
    {
        var setting = await _db.VaultSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is not null)
        {
            _db.VaultSettings.Remove(setting);
            await _db.SaveChangesAsync(ct);
        }
    }

    // ─── Audit Logs ───────────────────────────────────────

    public async Task<List<VaultAuditLog>> GetAuditLogsAsync(int page = 1, int pageSize = 50, string? action = null, CancellationToken ct = default)
    {
        var query = _db.VaultAuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);
        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task AddAuditLogAsync(VaultAuditLog log, CancellationToken ct = default)
    {
        await _db.VaultAuditLogs.AddAsync(log, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> GetAuditLogCountAsync(string? action = null, CancellationToken ct = default)
    {
        var query = _db.VaultAuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);
        return await query.CountAsync(ct);
    }

    // ─── Encryption Configs ───────────────────────────

    public async Task<List<EncryptionConfig>> GetAllEncryptionConfigsAsync(CancellationToken ct = default)
        => await _db.EncryptionConfigs.OrderBy(c => c.TableName).ToListAsync(ct);

    public async Task<EncryptionConfig?> GetEncryptionConfigByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.EncryptionConfigs.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddEncryptionConfigAsync(EncryptionConfig config, CancellationToken ct = default)
    {
        await _db.EncryptionConfigs.AddAsync(config, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateEncryptionConfigAsync(EncryptionConfig config, CancellationToken ct = default)
    {
        _db.EncryptionConfigs.Update(config);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteEncryptionConfigAsync(Guid id, CancellationToken ct = default)
    {
        var config = await _db.EncryptionConfigs.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (config is not null)
        {
            _db.EncryptionConfigs.Remove(config);
            await _db.SaveChangesAsync(ct);
        }
    }

    // ─── Field Access Policies ────────────────────────

    public async Task<List<FieldAccessPolicy>> GetAllFieldAccessPoliciesAsync(CancellationToken ct = default)
        => await _db.FieldAccessPolicies.OrderBy(p => p.TableName).ThenBy(p => p.FieldName).ToListAsync(ct);

    public async Task<FieldAccessPolicy?> GetFieldAccessPolicyByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.FieldAccessPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddFieldAccessPolicyAsync(FieldAccessPolicy policy, CancellationToken ct = default)
    {
        await _db.FieldAccessPolicies.AddAsync(policy, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateFieldAccessPolicyAsync(FieldAccessPolicy policy, CancellationToken ct = default)
    {
        _db.FieldAccessPolicies.Update(policy);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteFieldAccessPolicyAsync(Guid id, CancellationToken ct = default)
    {
        var policy = await _db.FieldAccessPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy is not null)
        {
            _db.FieldAccessPolicies.Remove(policy);
            await _db.SaveChangesAsync(ct);
        }
    }

    // ─── Secret Rotation Schedules ────────────────────

    public async Task<List<SecretRotationSchedule>> GetRotationSchedulesAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        var query = _db.SecretRotationSchedules.AsNoTracking().AsQueryable();
        if (activeOnly)
            query = query.Where(s => s.IsActive);
        return await query.OrderBy(s => s.NextRotationAt).ToListAsync(ct);
    }

    public async Task<SecretRotationSchedule?> GetRotationScheduleByPathAsync(string secretPath, CancellationToken ct = default)
        => await _db.SecretRotationSchedules.FirstOrDefaultAsync(s => s.SecretPath == secretPath, ct);

    public async Task AddRotationScheduleAsync(SecretRotationSchedule schedule, CancellationToken ct = default)
    {
        await _db.SecretRotationSchedules.AddAsync(schedule, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateRotationScheduleAsync(SecretRotationSchedule schedule, CancellationToken ct = default)
    {
        _db.SecretRotationSchedules.Update(schedule);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteRotationScheduleAsync(string secretPath, CancellationToken ct = default)
    {
        var schedule = await _db.SecretRotationSchedules.FirstOrDefaultAsync(s => s.SecretPath == secretPath, ct);
        if (schedule is not null)
        {
            _db.SecretRotationSchedules.Remove(schedule);
            await _db.SaveChangesAsync(ct);
        }
    }
}
