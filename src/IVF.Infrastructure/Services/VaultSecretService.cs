using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Manages vault secrets with AES-256-GCM encryption at rest in PostgreSQL.
/// KEK is a random 256-bit key wrapped/unwrapped via IKeyVaultService
/// (Azure RSA-OAEP-256 or local AES-256-GCM fallback).
/// On first run, migrates from legacy PBKDF2-derived KEK to wrapped random KEK.
/// </summary>
public class VaultSecretService : IVaultSecretService
{
    private readonly IVaultRepository _repo;
    private readonly IKeyVaultService _keyVaultService;
    private readonly IConfiguration _config;
    private readonly ILogger<VaultSecretService> _logger;

    // Static cache so KEK is unwrapped once across all scoped instances
    private static byte[]? s_kek;
    private static readonly SemaphoreSlim s_kekLock = new(1, 1);

    private const int IvLength = 12;
    private const int TagLength = 16;
    private const int KeyLength = 32; // 256-bit
    private const int Pbkdf2Iterations = 100_000;

    private const string WrappedKekSettingKey = "vault-secret-wrapped-kek";
    private const string WrappedKekIvSettingKey = "vault-secret-wrapped-kek-iv";
    private const string KekKeyName = "vault-secret-kek";

    public VaultSecretService(
        IVaultRepository repo,
        IKeyVaultService keyVaultService,
        IConfiguration config,
        ILogger<VaultSecretService> logger)
    {
        _repo = repo;
        _keyVaultService = keyVaultService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Lazily initializes the KEK: unwraps from DB if available, or migrates
    /// from legacy PBKDF2-derived KEK to a new random KEK wrapped via Azure KV.
    /// </summary>
    private async Task<byte[]> GetKekAsync()
    {
        if (s_kek is not null) return s_kek;

        await s_kekLock.WaitAsync();
        try
        {
            if (s_kek is not null) return s_kek;

            var wrappedKekSetting = await _repo.GetSettingAsync(WrappedKekSettingKey);
            var wrappedIvSetting = await _repo.GetSettingAsync(WrappedKekIvSettingKey);

            if (wrappedKekSetting is not null && !string.IsNullOrEmpty(wrappedKekSetting.ValueJson))
            {
                // Unwrap existing KEK via Key Vault
                var wrappedBase64 = DeserializeSettingValue(wrappedKekSetting.ValueJson);
                var ivBase64 = wrappedIvSetting is not null
                    ? DeserializeSettingValue(wrappedIvSetting.ValueJson)
                    : "";
                s_kek = await _keyVaultService.UnwrapKeyAsync(wrappedBase64, ivBase64, KekKeyName);
                _logger.LogInformation("Vault secret KEK unwrapped via Key Vault");
            }
            else
            {
                // First run with new code: migrate from legacy KEK to wrapped random KEK
                await MigrateToWrappedKekAsync();
            }

            return s_kek!;
        }
        finally
        {
            s_kekLock.Release();
        }
    }

    /// <summary>
    /// One-time migration: generate new random KEK, re-encrypt all existing secrets,
    /// wrap the new KEK via Azure KV, and store the wrapped KEK in DB.
    /// </summary>
    private async Task MigrateToWrappedKekAsync()
    {
        // Derive the LEGACY KEK (same formula as previous code)
        var masterKey = _config["JwtSettings:Secret"] ?? "IVF-Vault-Default-Key";
        var salt = Encoding.UTF8.GetBytes("IVF-Vault-KEK-Salt-2026");
        var oldKek = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(masterKey),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeyLength);

        // Generate NEW random KEK
        var newKek = RandomNumberGenerator.GetBytes(KeyLength);

        // Re-encrypt all existing secrets from old KEK → new KEK
        var allSecrets = await _repo.ListSecretsAsync(null);
        var migratedCount = 0;
        foreach (var secret in allSecrets)
        {
            try
            {
                var plaintext = DecryptWithKey(secret.EncryptedData, secret.Iv, oldKek);
                var (ciphertext, iv) = EncryptWithKey(plaintext, newKek);
                secret.UpdateData(ciphertext, iv);
                await _repo.UpdateSecretAsync(secret);
                migratedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to migrate secret {Path} — skipping", secret.Path);
            }
        }

        // Wrap new KEK via Key Vault and persist
        var wrapped = await _keyVaultService.WrapKeyAsync(newKek, KekKeyName);
        await _repo.SaveSettingAsync(WrappedKekSettingKey, JsonSerializer.Serialize(wrapped.WrappedKeyBase64));
        await _repo.SaveSettingAsync(WrappedKekIvSettingKey, JsonSerializer.Serialize(wrapped.IvBase64));

        s_kek = newKek;
        _logger.LogInformation(
            "Vault KEK migrated to Key Vault wrapped key ({Algorithm}). {Count} secrets re-encrypted",
            wrapped.Algorithm, migratedCount);
    }

    private static string DeserializeSettingValue(string json)
    {
        try { return JsonSerializer.Deserialize<string>(json) ?? json; }
        catch { return json; }
    }

    public async Task<VaultSecretResult?> GetSecretAsync(string path, int? version = null, CancellationToken ct = default)
    {
        var secret = await _repo.GetSecretAsync(path, version, ct);
        if (secret is null) return null;

        var kek = await GetKekAsync();
        var plaintext = DecryptWithKey(secret.EncryptedData, secret.Iv, kek);
        return new VaultSecretResult(
            secret.Id, secret.Path, secret.Version, plaintext,
            secret.Metadata, secret.CreatedAt, secret.UpdatedAt);
    }

    public async Task<VaultSecretResult> PutSecretAsync(string path, string plaintext, Guid? userId = null, string? metadata = null, CancellationToken ct = default)
    {
        var kek = await GetKekAsync();
        var (ciphertext, iv) = EncryptWithKey(plaintext, kek);
        var latestVersion = await _repo.GetLatestVersionAsync(path, ct);
        var newVersion = latestVersion + 1;

        var metadataJson = metadata ?? JsonSerializer.Serialize(new
        {
            versions = newVersion,
            maxVersions = 10
        });

        var secret = VaultSecret.Create(path, ciphertext, iv, userId, metadataJson, newVersion);
        await _repo.AddSecretAsync(secret, ct);

        _logger.LogInformation("Vault secret created: {Path} v{Version}", path, newVersion);

        return new VaultSecretResult(
            secret.Id, path, newVersion, plaintext,
            metadataJson, secret.CreatedAt, null);
    }

    public async Task DeleteSecretAsync(string path, CancellationToken ct = default)
    {
        await _repo.DeleteSecretAsync(path, ct);
        _logger.LogInformation("Vault secret deleted: {Path}", path);
    }

    public async Task<IEnumerable<VaultSecretEntry>> ListSecretsAsync(string? prefix = null, CancellationToken ct = default)
    {
        var secrets = await _repo.ListSecretsAsync(prefix, ct);
        var entries = new List<VaultSecretEntry>();
        var folders = new HashSet<string>();

        foreach (var secret in secrets)
        {
            var relativePath = secret.Path;
            if (!string.IsNullOrEmpty(prefix))
            {
                var trimmedPrefix = prefix.TrimEnd('/');
                if (relativePath.StartsWith(trimmedPrefix + "/"))
                    relativePath = relativePath[(trimmedPrefix.Length + 1)..];
                else if (relativePath.StartsWith(trimmedPrefix + "-"))
                    relativePath = relativePath[(trimmedPrefix.Length + 1)..];
            }

            var slashIdx = relativePath.IndexOf('/');
            if (slashIdx > 0)
            {
                var folder = relativePath[..slashIdx] + "/";
                if (folders.Add(folder))
                    entries.Add(new VaultSecretEntry(folder, "folder"));
            }
            else
            {
                entries.Add(new VaultSecretEntry(relativePath, "secret"));
            }
        }

        return entries;
    }

    public async Task<IEnumerable<VaultSecretVersionInfo>> GetVersionsAsync(string path, CancellationToken ct = default)
    {
        var versions = await _repo.GetSecretVersionsAsync(path, ct);
        return versions.Select(v => new VaultSecretVersionInfo(
            v.Version, v.CreatedAt, v.DeletedAt));
    }

    public async Task<VaultImportResult> ImportSecretsAsync(
        Dictionary<string, string> secrets,
        string? prefix = null,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        var imported = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var (key, value) in secrets)
        {
            try
            {
                var path = string.IsNullOrEmpty(prefix) ? key : $"{prefix.TrimEnd('/')}/{key}";
                await PutSecretAsync(path, value, userId, null, ct);
                imported++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{key}: {ex.Message}");
            }
        }

        return new VaultImportResult(imported, failed, errors);
    }

    // ─── AES-256-GCM Encryption ──────────────────────────

    private static (string CiphertextBase64, string IvBase64) EncryptWithKey(string plaintext, byte[] key)
    {
        var iv = RandomNumberGenerator.GetBytes(IvLength);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagLength];

        using var aes = new AesGcm(key, TagLength);
        aes.Encrypt(iv, plaintextBytes, ciphertext, tag);

        // Combine ciphertext + tag
        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

        return (Convert.ToBase64String(combined), Convert.ToBase64String(iv));
    }

    private static string DecryptWithKey(string ciphertextBase64, string ivBase64, byte[] key)
    {
        var combined = Convert.FromBase64String(ciphertextBase64);
        var iv = Convert.FromBase64String(ivBase64);

        var ciphertext = combined[..^TagLength];
        var tag = combined[^TagLength..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagLength);
        aes.Decrypt(iv, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
