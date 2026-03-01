using System.Security.Cryptography;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services.Kms;

/// <summary>
/// Local KMS provider using AES-256-GCM for all operations.
/// Keys are stored as vault secrets via IVaultRepository settings.
/// Suitable for development, single-node, and on-premise deployments.
/// </summary>
public class LocalKmsProvider : IKmsProvider
{
    private readonly IVaultRepository _vaultRepo;
    private readonly ILogger<LocalKmsProvider> _logger;

    private const int KeyLength = 32;
    private const int IvLength = 12;
    private const int TagLength = 16;

    public string ProviderName => "Local";

    public LocalKmsProvider(IVaultRepository vaultRepo, ILogger<LocalKmsProvider> logger)
    {
        _vaultRepo = vaultRepo;
        _logger = logger;
    }

    public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(true);

    public async Task<KmsKeyInfo> CreateKeyAsync(KmsCreateKeyRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.KeyName);

        var settingKey = $"kms-key-{request.KeyName}";
        var existing = await _vaultRepo.GetSettingAsync(settingKey, ct);
        if (existing is not null)
            throw new InvalidOperationException($"Key '{request.KeyName}' already exists");

        var keyBytes = RandomNumberGenerator.GetBytes(KeyLength);
        var meta = new LocalKeyMetadata
        {
            KeyName = request.KeyName,
            KeyType = request.KeyType,
            Version = 1,
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            KeyBase64 = Convert.ToBase64String(keyBytes),
            Tags = request.Tags
        };

        await _vaultRepo.SaveSettingAsync(settingKey, JsonSerializer.Serialize(meta), ct);
        _logger.LogInformation("Created local KMS key: {KeyName}", request.KeyName);

        return ToKeyInfo(meta);
    }

    public async Task<KmsKeyInfo?> GetKeyInfoAsync(string keyName, CancellationToken ct = default)
    {
        var meta = await LoadKeyAsync(keyName, ct);
        return meta is null ? null : ToKeyInfo(meta);
    }

    public async Task<IReadOnlyList<KmsKeyInfo>> ListKeysAsync(CancellationToken ct = default)
    {
        var allSettings = await _vaultRepo.GetAllSettingsAsync(ct);
        var keys = new List<KmsKeyInfo>();

        foreach (var s in allSettings.Where(s => s.Key.StartsWith("kms-key-")))
        {
            var meta = JsonSerializer.Deserialize<LocalKeyMetadata>(s.ValueJson);
            if (meta is not null)
                keys.Add(ToKeyInfo(meta));
        }

        return keys;
    }

    public async Task<KmsKeyInfo> RotateKeyAsync(string keyName, CancellationToken ct = default)
    {
        var meta = await LoadKeyAsync(keyName, ct)
            ?? throw new InvalidOperationException($"Key '{keyName}' not found");

        // Archive old version
        var archiveKey = $"kms-key-{keyName}-v{meta.Version}";
        await _vaultRepo.SaveSettingAsync(archiveKey, JsonSerializer.Serialize(meta), ct);

        // Generate new key material
        meta.KeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyLength));
        meta.Version++;
        meta.RotatedAt = DateTime.UtcNow;

        var settingKey = $"kms-key-{keyName}";
        await _vaultRepo.SaveSettingAsync(settingKey, JsonSerializer.Serialize(meta), ct);

        _logger.LogInformation("Rotated local KMS key: {KeyName} to version {Version}", keyName, meta.Version);
        return ToKeyInfo(meta);
    }

    public async Task<KmsEncryptResult> EncryptAsync(string keyName, byte[] plaintext, CancellationToken ct = default)
    {
        var meta = await LoadKeyAsync(keyName, ct)
            ?? throw new InvalidOperationException($"Key '{keyName}' not found");

        var key = Convert.FromBase64String(meta.KeyBase64);
        var iv = RandomNumberGenerator.GetBytes(IvLength);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];

        using var aes = new AesGcm(key, TagLength);
        aes.Encrypt(iv, plaintext, ciphertext, tag);

        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

        return new KmsEncryptResult(combined, iv, keyName, meta.Version, "AES-256-GCM");
    }

    public async Task<byte[]> DecryptAsync(string keyName, byte[] ciphertext, byte[] iv, CancellationToken ct = default)
    {
        var meta = await LoadKeyAsync(keyName, ct)
            ?? throw new InvalidOperationException($"Key '{keyName}' not found");

        var key = Convert.FromBase64String(meta.KeyBase64);

        if (ciphertext.Length < TagLength)
            throw new ArgumentException("Ciphertext too short for AES-GCM tag");

        var ct_data = ciphertext.AsSpan(0, ciphertext.Length - TagLength).ToArray();
        var tag = ciphertext.AsSpan(ciphertext.Length - TagLength).ToArray();
        var plaintext = new byte[ct_data.Length];

        using var aes = new AesGcm(key, TagLength);
        aes.Decrypt(iv, ct_data, tag, plaintext);

        return plaintext;
    }

    public async Task<KmsWrapResult> WrapKeyAsync(string keyName, byte[] keyToWrap, CancellationToken ct = default)
    {
        var result = await EncryptAsync(keyName, keyToWrap, ct);
        return new KmsWrapResult(result.Ciphertext, result.Iv, result.KeyName, result.KeyVersion, result.Algorithm);
    }

    public async Task<byte[]> UnwrapKeyAsync(string keyName, byte[] wrappedKey, byte[] iv, CancellationToken ct = default)
    {
        return await DecryptAsync(keyName, wrappedKey, iv, ct);
    }

    // ─── Helpers ──────────────────────────────────────────

    private async Task<LocalKeyMetadata?> LoadKeyAsync(string keyName, CancellationToken ct)
    {
        var settingKey = $"kms-key-{keyName}";
        var setting = await _vaultRepo.GetSettingAsync(settingKey, ct);
        if (setting is null) return null;
        return JsonSerializer.Deserialize<LocalKeyMetadata>(setting.ValueJson);
    }

    private static KmsKeyInfo ToKeyInfo(LocalKeyMetadata meta) => new(
        meta.KeyName, meta.KeyType, meta.Version, meta.Enabled,
        meta.CreatedAt, meta.RotatedAt, "Local", meta.Tags);

    private sealed class LocalKeyMetadata
    {
        public string KeyName { get; set; } = "";
        public KmsKeyType KeyType { get; set; } = KmsKeyType.Aes256;
        public int Version { get; set; } = 1;
        public bool Enabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RotatedAt { get; set; }
        public string KeyBase64 { get; set; } = "";
        public Dictionary<string, string>? Tags { get; set; }
    }
}
