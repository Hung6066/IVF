using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IKeyVaultService
{
    /// <summary>Get secret from Key Vault by name</summary>
    Task<string> GetSecretAsync(string secretName, CancellationToken ct = default);

    /// <summary>Set secret in Key Vault</summary>
    Task SetSecretAsync(string secretName, string secretValue, CancellationToken ct = default);

    /// <summary>Get secret with specific version</summary>
    Task<string> GetSecretVersionAsync(string secretName, string version, CancellationToken ct = default);

    /// <summary>Delete a secret (soft delete)</summary>
    Task DeleteSecretAsync(string secretName, CancellationToken ct = default);

    /// <summary>List all secret names</summary>
    Task<IEnumerable<string>> ListSecretsAsync(CancellationToken ct = default);

    /// <summary>Check if Key Vault is accessible</summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);

    // ─── Key Wrap / Unwrap (KEK → DEK envelope encryption) ───

    /// <summary>Wrap (encrypt) a DEK using the vault's KEK via AES-256-GCM or Azure RSA-OAEP-256</summary>
    Task<WrappedKeyResult> WrapKeyAsync(byte[] keyToWrap, string keyName, CancellationToken ct = default);

    /// <summary>Unwrap (decrypt) a previously wrapped DEK</summary>
    Task<byte[]> UnwrapKeyAsync(string wrappedKeyBase64, string ivBase64, string keyName, CancellationToken ct = default);

    /// <summary>Encrypt plaintext using a DEK identified by purpose</summary>
    Task<EncryptedPayload> EncryptAsync(byte[] plaintext, KeyPurpose purpose, CancellationToken ct = default);

    /// <summary>Decrypt ciphertext using a DEK identified by purpose</summary>
    Task<byte[]> DecryptAsync(string ciphertextBase64, string ivBase64, KeyPurpose purpose, CancellationToken ct = default);

    /// <summary>Get the current auto-unseal configuration status</summary>
    Task<AutoUnsealStatus> GetAutoUnsealStatusAsync(CancellationToken ct = default);

    /// <summary>Configure auto-unseal: wrap the master password with Azure KV RSA key and store it</summary>
    Task<bool> ConfigureAutoUnsealAsync(string masterPassword, string azureKeyName, CancellationToken ct = default);

    /// <summary>Auto-unseal the vault using the stored wrapped master password</summary>
    Task<bool> AutoUnsealAsync(CancellationToken ct = default);
}

// ─── Result Types ─────────────────────────────────

public record WrappedKeyResult(
    string WrappedKeyBase64,
    string IvBase64,
    string Algorithm,
    string KeyName,
    int KeyVersion);

public record EncryptedPayload(
    string CiphertextBase64,
    string IvBase64,
    KeyPurpose Purpose,
    string Algorithm);

public record AutoUnsealStatus(
    bool IsConfigured,
    string? KeyVaultUrl,
    string? KeyName,
    string? Algorithm,
    DateTime? ConfiguredAt);
