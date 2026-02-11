using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Stores a user's handwritten signature image and digital certificate info
/// for PDF document signing. Each user can have one active signature.
/// 
/// Flow: User draws signature on canvas → base64 PNG stored → used as visible
/// stamp on digitally signed PDFs via SignServer.
/// </summary>
public class UserSignature : BaseEntity
{
    /// <summary>FK to User who owns this signature.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Base64-encoded PNG image of the handwritten signature.</summary>
    public string SignatureImageBase64 { get; private set; } = string.Empty;

    /// <summary>MIME type of the signature image (default: image/png).</summary>
    public string ImageMimeType { get; private set; } = "image/png";

    /// <summary>Whether this signature is currently active for signing.</summary>
    public bool IsActive { get; private set; } = true;

    // ─── Certificate Information ────────────────────────────────

    /// <summary>Common Name on the user's signing certificate (e.g., "Dr. Nguyen Van A").</summary>
    public string? CertificateSubject { get; private set; }

    /// <summary>Certificate serial number from EJBCA.</summary>
    public string? CertificateSerialNumber { get; private set; }

    /// <summary>Certificate expiry date.</summary>
    public DateTime? CertificateExpiry { get; private set; }

    /// <summary>SignServer worker name assigned to this user (null = use default).</summary>
    public string? WorkerName { get; private set; }

    /// <summary>PKCS12 keystore path inside SignServer container.</summary>
    public string? KeystorePath { get; private set; }

    /// <summary>Status of certificate provisioning.</summary>
    public CertificateStatus CertStatus { get; private set; } = CertificateStatus.None;

    /// <summary>Navigation property.</summary>
    public User? User { get; private set; }

    private UserSignature() { }

    public static UserSignature Create(
        Guid userId,
        string signatureImageBase64,
        string imageMimeType = "image/png")
    {
        return new UserSignature
        {
            UserId = userId,
            SignatureImageBase64 = signatureImageBase64,
            ImageMimeType = imageMimeType,
            IsActive = true,
            CertStatus = CertificateStatus.None
        };
    }

    public void UpdateSignatureImage(string signatureImageBase64, string imageMimeType = "image/png")
    {
        SignatureImageBase64 = signatureImageBase64;
        ImageMimeType = imageMimeType;
        SetUpdated();
    }

    public void SetCertificateInfo(
        string subject,
        string? serialNumber,
        DateTime? expiry,
        string? workerName,
        string? keystorePath)
    {
        CertificateSubject = subject;
        CertificateSerialNumber = serialNumber;
        CertificateExpiry = expiry;
        WorkerName = workerName;
        KeystorePath = keystorePath;
        CertStatus = CertificateStatus.Active;
        SetUpdated();
    }

    public void SetCertificateStatus(CertificateStatus status)
    {
        CertStatus = status;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }
}

/// <summary>
/// Status of the user's signing certificate lifecycle.
/// </summary>
public enum CertificateStatus
{
    None = 0,
    Pending = 1,
    Active = 2,
    Expired = 3,
    Revoked = 4,
    Error = 5
}
