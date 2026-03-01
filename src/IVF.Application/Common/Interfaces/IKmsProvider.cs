namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Provider-agnostic KMS abstraction for key management operations.
/// Implementations: Azure Key Vault, AWS KMS, HashiCorp Vault, Local (AES).
/// Selected via configuration: "KmsProvider": "Azure|AWS|HashiCorp|Local"
/// </summary>
public interface IKmsProvider
{
    /// <summary>Provider name for identification.</summary>
    string ProviderName { get; }

    /// <summary>Check if the KMS provider is accessible.</summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);

    // ─── Key Operations ─────────────────────────────────

    /// <summary>Create a new encryption key in the KMS.</summary>
    Task<KmsKeyInfo> CreateKeyAsync(KmsCreateKeyRequest request, CancellationToken ct = default);

    /// <summary>Get key metadata (not the key material itself).</summary>
    Task<KmsKeyInfo?> GetKeyInfoAsync(string keyName, CancellationToken ct = default);

    /// <summary>List all managed keys.</summary>
    Task<IReadOnlyList<KmsKeyInfo>> ListKeysAsync(CancellationToken ct = default);

    /// <summary>Rotate a key (create new version, old versions stay for decryption).</summary>
    Task<KmsKeyInfo> RotateKeyAsync(string keyName, CancellationToken ct = default);

    // ─── Encrypt / Decrypt ──────────────────────────────

    /// <summary>Encrypt data using a named key.</summary>
    Task<KmsEncryptResult> EncryptAsync(string keyName, byte[] plaintext, CancellationToken ct = default);

    /// <summary>Decrypt data using a named key.</summary>
    Task<byte[]> DecryptAsync(string keyName, byte[] ciphertext, byte[] iv, CancellationToken ct = default);

    // ─── Key Wrap / Unwrap (Envelope Encryption) ────────

    /// <summary>Wrap (encrypt) a DEK using a KEK in the KMS.</summary>
    Task<KmsWrapResult> WrapKeyAsync(string keyName, byte[] keyToWrap, CancellationToken ct = default);

    /// <summary>Unwrap (decrypt) a previously wrapped key.</summary>
    Task<byte[]> UnwrapKeyAsync(string keyName, byte[] wrappedKey, byte[] iv, CancellationToken ct = default);
}

// ─── Request/Result Types ─────────────────────────────────

public record KmsCreateKeyRequest(
    string KeyName,
    KmsKeyType KeyType = KmsKeyType.Aes256,
    bool Exportable = false,
    Dictionary<string, string>? Tags = null);

public enum KmsKeyType
{
    Aes256,
    Rsa2048,
    Rsa4096,
    EcP256,
    EcP384
}

public record KmsKeyInfo(
    string KeyName,
    KmsKeyType KeyType,
    int Version,
    bool Enabled,
    DateTime CreatedAt,
    DateTime? RotatedAt,
    string Provider,
    Dictionary<string, string>? Tags = null);

public record KmsEncryptResult(
    byte[] Ciphertext,
    byte[] Iv,
    string KeyName,
    int KeyVersion,
    string Algorithm);

public record KmsWrapResult(
    byte[] WrappedKey,
    byte[] Iv,
    string KeyName,
    int KeyVersion,
    string Algorithm);
