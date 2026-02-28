using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IVaultRepository
{
    // ─── Secrets ──────────────────────────────────────────
    Task<VaultSecret?> GetSecretAsync(string path, int? version = null, CancellationToken ct = default);
    Task<VaultSecret?> GetSecretByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<VaultSecret>> ListSecretsAsync(string? pathPrefix = null, CancellationToken ct = default);
    Task<int> GetLatestVersionAsync(string path, CancellationToken ct = default);
    Task AddSecretAsync(VaultSecret secret, CancellationToken ct = default);
    Task UpdateSecretAsync(VaultSecret secret, CancellationToken ct = default);
    Task DeleteSecretAsync(string path, CancellationToken ct = default);
    Task<List<VaultSecret>> GetSecretVersionsAsync(string path, CancellationToken ct = default);

    // ─── Policies ─────────────────────────────────────────
    Task<List<VaultPolicy>> GetPoliciesAsync(CancellationToken ct = default);
    Task<VaultPolicy?> GetPolicyByIdAsync(Guid id, CancellationToken ct = default);
    Task<VaultPolicy?> GetPolicyByNameAsync(string name, CancellationToken ct = default);
    Task AddPolicyAsync(VaultPolicy policy, CancellationToken ct = default);
    Task UpdatePolicyAsync(VaultPolicy policy, CancellationToken ct = default);
    Task DeletePolicyAsync(Guid id, CancellationToken ct = default);

    // ─── User Policies ────────────────────────────────────
    Task<List<VaultUserPolicy>> GetUserPoliciesAsync(CancellationToken ct = default);
    Task<List<VaultUserPolicy>> GetUserPoliciesByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddUserPolicyAsync(VaultUserPolicy userPolicy, CancellationToken ct = default);
    Task RemoveUserPolicyAsync(Guid id, CancellationToken ct = default);

    // ─── Leases ───────────────────────────────────────────
    Task<List<VaultLease>> GetLeasesAsync(bool includeExpired = false, CancellationToken ct = default);
    Task<VaultLease?> GetLeaseByIdAsync(string leaseId, CancellationToken ct = default);
    Task AddLeaseAsync(VaultLease lease, CancellationToken ct = default);
    Task UpdateLeaseAsync(VaultLease lease, CancellationToken ct = default);
    Task RevokeLeaseAsync(string leaseId, CancellationToken ct = default);

    // ─── Dynamic Credentials ──────────────────────────────
    Task<List<VaultDynamicCredential>> GetDynamicCredentialsAsync(bool includeRevoked = false, CancellationToken ct = default);
    Task<VaultDynamicCredential?> GetDynamicCredentialByIdAsync(Guid id, CancellationToken ct = default);
    Task AddDynamicCredentialAsync(VaultDynamicCredential credential, CancellationToken ct = default);
    Task RevokeDynamicCredentialAsync(Guid id, CancellationToken ct = default);

    // ─── Tokens ───────────────────────────────────────────
    Task<List<VaultToken>> GetTokensAsync(bool includeRevoked = false, CancellationToken ct = default);
    Task<VaultToken?> GetTokenByIdAsync(Guid id, CancellationToken ct = default);
    Task<VaultToken?> GetTokenByHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddTokenAsync(VaultToken token, CancellationToken ct = default);
    Task UpdateTokenAsync(VaultToken token, CancellationToken ct = default);
    Task RevokeTokenAsync(Guid id, CancellationToken ct = default);

    // ─── Auto-Unseal ─────────────────────────────────────
    Task<VaultAutoUnseal?> GetAutoUnsealConfigAsync(CancellationToken ct = default);
    Task SaveAutoUnsealConfigAsync(VaultAutoUnseal config, CancellationToken ct = default);

    // ─── Settings ─────────────────────────────────────────
    Task<VaultSetting?> GetSettingAsync(string key, CancellationToken ct = default);
    Task<List<VaultSetting>> GetAllSettingsAsync(CancellationToken ct = default);
    Task SaveSettingAsync(string key, string valueJson, CancellationToken ct = default);
    Task DeleteSettingAsync(string key, CancellationToken ct = default);

    // ─── Audit Logs ───────────────────────────────────────
    Task<List<VaultAuditLog>> GetAuditLogsAsync(int page = 1, int pageSize = 50, string? action = null, CancellationToken ct = default);
    Task AddAuditLogAsync(VaultAuditLog log, CancellationToken ct = default);
    Task<int> GetAuditLogCountAsync(string? action = null, CancellationToken ct = default);

    // ─── Encryption Configs ───────────────────────────────
    Task<List<EncryptionConfig>> GetAllEncryptionConfigsAsync(CancellationToken ct = default);
    Task<EncryptionConfig?> GetEncryptionConfigByIdAsync(Guid id, CancellationToken ct = default);
    Task AddEncryptionConfigAsync(EncryptionConfig config, CancellationToken ct = default);
    Task UpdateEncryptionConfigAsync(EncryptionConfig config, CancellationToken ct = default);
    Task DeleteEncryptionConfigAsync(Guid id, CancellationToken ct = default);

    // ─── Field Access Policies ────────────────────────────
    Task<List<FieldAccessPolicy>> GetAllFieldAccessPoliciesAsync(CancellationToken ct = default);
    Task<FieldAccessPolicy?> GetFieldAccessPolicyByIdAsync(Guid id, CancellationToken ct = default);
    Task AddFieldAccessPolicyAsync(FieldAccessPolicy policy, CancellationToken ct = default);
    Task UpdateFieldAccessPolicyAsync(FieldAccessPolicy policy, CancellationToken ct = default);
    Task DeleteFieldAccessPolicyAsync(Guid id, CancellationToken ct = default);
}
