using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Stores wrapped master key for Azure Key Vault auto-unseal.
/// Azure KV is ONLY used for wrap/unwrap of the master key.
/// </summary>
public class VaultAutoUnseal : BaseEntity
{
    public string WrappedKey { get; private set; } = string.Empty; // Base64 wrapped master key
    public string KeyVaultUrl { get; private set; } = string.Empty;
    public string KeyName { get; private set; } = string.Empty;
    public string? KeyVersion { get; private set; }
    public string Algorithm { get; private set; } = "RSA-OAEP-256";
    public string? Iv { get; private set; } // For local wrap mode
    public Guid? CreatedBy { get; private set; }

    private VaultAutoUnseal() { }

    public static VaultAutoUnseal Create(
        string wrappedKey,
        string keyVaultUrl,
        string keyName,
        string algorithm = "RSA-OAEP-256",
        string? iv = null,
        string? keyVersion = null,
        Guid? createdBy = null)
    {
        return new VaultAutoUnseal
        {
            WrappedKey = wrappedKey,
            KeyVaultUrl = keyVaultUrl,
            KeyName = keyName,
            Algorithm = algorithm,
            Iv = iv,
            KeyVersion = keyVersion,
            CreatedBy = createdBy
        };
    }

    public void UpdateWrappedKey(string wrappedKey, string? iv = null, string? keyVersion = null)
    {
        WrappedKey = wrappedKey;
        if (iv is not null) Iv = iv;
        if (keyVersion is not null) KeyVersion = keyVersion;
        SetUpdated();
    }
}
