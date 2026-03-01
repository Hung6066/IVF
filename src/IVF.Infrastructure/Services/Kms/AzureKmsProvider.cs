using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services.Kms;

/// <summary>
/// Azure Key Vault KMS provider — delegates to the existing IKeyVaultService
/// for actual cryptographic operations. This adapter bridges the provider-agnostic
/// IKmsProvider interface to Azure-specific functionality.
/// </summary>
public class AzureKmsProvider : IKmsProvider
{
    private readonly IKeyVaultService _kvService;
    private readonly IVaultRepository _vaultRepo;
    private readonly ILogger<AzureKmsProvider> _logger;

    public string ProviderName => "Azure";

    public AzureKmsProvider(
        IKeyVaultService kvService,
        IVaultRepository vaultRepo,
        ILogger<AzureKmsProvider> logger)
    {
        _kvService = kvService;
        _vaultRepo = vaultRepo;
        _logger = logger;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        return await _kvService.IsHealthyAsync(ct);
    }

    public async Task<KmsKeyInfo> CreateKeyAsync(KmsCreateKeyRequest request, CancellationToken ct = default)
    {
        // Azure KV manages keys internally; we store the key as a secret
        var keyBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var secretName = $"kms-{request.KeyName}";
        await _kvService.SetSecretAsync(secretName, Convert.ToBase64String(keyBytes), ct);

        _logger.LogInformation("Created Azure KMS key: {KeyName}", request.KeyName);

        return new KmsKeyInfo(request.KeyName, request.KeyType, 1, true, DateTime.UtcNow, null, ProviderName, request.Tags);
    }

    public async Task<KmsKeyInfo?> GetKeyInfoAsync(string keyName, CancellationToken ct = default)
    {
        var secretName = $"kms-{keyName}";
        try
        {
            var value = await _kvService.GetSecretAsync(secretName, ct);
            if (string.IsNullOrEmpty(value)) return null;
            return new KmsKeyInfo(keyName, KmsKeyType.Aes256, 1, true, DateTime.UtcNow, null, ProviderName);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<KmsKeyInfo>> ListKeysAsync(CancellationToken ct = default)
    {
        var secrets = await _kvService.ListSecretsAsync(ct);
        return secrets
            .Where(s => s.StartsWith("kms-"))
            .Select(s => new KmsKeyInfo(s.Replace("kms-", ""), KmsKeyType.Aes256, 1, true, DateTime.UtcNow, null, ProviderName))
            .ToList();
    }

    public async Task<KmsKeyInfo> RotateKeyAsync(string keyName, CancellationToken ct = default)
    {
        var secretName = $"kms-{keyName}";
        var existing = await _kvService.GetSecretAsync(secretName, ct);
        if (string.IsNullOrEmpty(existing))
            throw new InvalidOperationException($"Key '{keyName}' not found in Azure KV");

        // Archive old version
        var version = await _vaultRepo.GetLatestVersionAsync(secretName, ct);
        var archiveName = $"kms-{keyName}-v{version}";
        await _kvService.SetSecretAsync(archiveName, existing, ct);

        // Create new key
        var newKeyBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        await _kvService.SetSecretAsync(secretName, Convert.ToBase64String(newKeyBytes), ct);

        var newVersion = version + 1;
        _logger.LogInformation("Rotated Azure KMS key: {KeyName} to version {Version}", keyName, newVersion);

        return new KmsKeyInfo(keyName, KmsKeyType.Aes256, newVersion, true, DateTime.UtcNow, DateTime.UtcNow, ProviderName);
    }

    public async Task<KmsEncryptResult> EncryptAsync(string keyName, byte[] plaintext, CancellationToken ct = default)
    {
        // Use existing IKeyVaultService encrypt with Data purpose (default)
        var purpose = Domain.Enums.KeyPurpose.Data;
        var result = await _kvService.EncryptAsync(plaintext, purpose, ct);

        return new KmsEncryptResult(
            Convert.FromBase64String(result.CiphertextBase64),
            Convert.FromBase64String(result.IvBase64),
            keyName, 1, result.Algorithm);
    }

    public async Task<byte[]> DecryptAsync(string keyName, byte[] ciphertext, byte[] iv, CancellationToken ct = default)
    {
        var purpose = Domain.Enums.KeyPurpose.Data;
        return await _kvService.DecryptAsync(
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(iv),
            purpose, ct);
    }

    public async Task<KmsWrapResult> WrapKeyAsync(string keyName, byte[] keyToWrap, CancellationToken ct = default)
    {
        var result = await _kvService.WrapKeyAsync(keyToWrap, keyName, ct);
        return new KmsWrapResult(
            Convert.FromBase64String(result.WrappedKeyBase64),
            Convert.FromBase64String(result.IvBase64),
            keyName, result.KeyVersion, result.Algorithm);
    }

    public async Task<byte[]> UnwrapKeyAsync(string keyName, byte[] wrappedKey, byte[] iv, CancellationToken ct = default)
    {
        return await _kvService.UnwrapKeyAsync(
            Convert.ToBase64String(wrappedKey),
            Convert.ToBase64String(iv),
            keyName, ct);
    }
}
