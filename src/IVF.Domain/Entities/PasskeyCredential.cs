using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// FIDO2/WebAuthn credential storage for a user.
/// Stores the public key credential after successful registration.
/// </summary>
public class PasskeyCredential : BaseEntity
{
    public Guid UserId { get; private set; }
    public string CredentialId { get; private set; } = string.Empty; // Base64url
    public string PublicKey { get; private set; } = string.Empty; // Base64
    public string UserHandle { get; private set; } = string.Empty; // Base64url
    public uint SignatureCounter { get; private set; }
    public string CredentialType { get; private set; } = "public-key";
    public string? DeviceName { get; private set; } // "Windows Hello", "Touch ID", "YubiKey"
    public string? AttestationFormat { get; private set; }
    public string? AaGuid { get; private set; } // Authenticator Attestation GUID
    public DateTime? LastUsedAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    private PasskeyCredential() { }

    public static PasskeyCredential Create(
        Guid userId,
        string credentialId,
        string publicKey,
        string userHandle,
        uint signatureCounter,
        string? deviceName = null,
        string? attestationFormat = null,
        string? aaGuid = null)
    {
        return new PasskeyCredential
        {
            UserId = userId,
            CredentialId = credentialId,
            PublicKey = publicKey,
            UserHandle = userHandle,
            SignatureCounter = signatureCounter,
            DeviceName = deviceName,
            AttestationFormat = attestationFormat,
            AaGuid = aaGuid,
            IsActive = true
        };
    }

    public void UpdateCounter(uint newCounter)
    {
        SignatureCounter = newCounter;
        LastUsedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Revoke()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Rename(string deviceName)
    {
        DeviceName = deviceName;
        SetUpdated();
    }
}
