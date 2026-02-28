using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// A certificate issued by a managed CA. Tracks server/client certs for mTLS,
/// PostgreSQL SSL, MinIO TLS, and inter-service communication.
/// </summary>
public class ManagedCertificate : BaseEntity
{
    public string CommonName { get; private set; } = null!;

    /// <summary>Comma-separated Subject Alternative Names (DNS/IP)</summary>
    public string? SubjectAltNames { get; private set; }

    public CertType Type { get; private set; } = CertType.Server;

    /// <summary>Purpose tag: pg-primary, pg-replica, minio-replica, api-client, etc.</summary>
    public string Purpose { get; private set; } = null!;

    /// <summary>PEM-encoded certificate</summary>
    public string CertificatePem { get; private set; } = null!;

    /// <summary>PEM-encoded private key</summary>
    public string PrivateKeyPem { get; private set; } = null!;

    /// <summary>SHA-256 fingerprint</summary>
    public string Fingerprint { get; private set; } = null!;

    /// <summary>Certificate serial number (hex)</summary>
    public string SerialNumber { get; private set; } = null!;

    public DateTime NotBefore { get; private set; }
    public DateTime NotAfter { get; private set; }

    public string KeyAlgorithm { get; private set; } = "RSA";
    public int KeySize { get; private set; } = 2048;

    /// <summary>The CA that issued this certificate</summary>
    public Guid IssuingCaId { get; private set; }
    public CertificateAuthority IssuingCa { get; private set; } = null!;

    public ManagedCertStatus Status { get; private set; } = ManagedCertStatus.Active;

    /// <summary>Deployment target (e.g., "primary-pg", "172.16.102.11")</summary>
    public string? DeployedTo { get; private set; }
    public DateTime? DeployedAt { get; private set; }

    /// <summary>Auto-renewal threshold in days before expiry</summary>
    public int RenewBeforeDays { get; private set; } = 30;

    /// <summary>Enable automatic renewal when expiry is within RenewBeforeDays</summary>
    public bool AutoRenewEnabled { get; private set; } = true;

    /// <summary>ID of the certificate this one replaced (rotation chain)</summary>
    public Guid? ReplacedCertId { get; private set; }

    /// <summary>ID of the certificate that replaced this one</summary>
    public Guid? ReplacedByCertId { get; private set; }

    /// <summary>Last auto-renewal attempt timestamp</summary>
    public DateTime? LastRenewalAttempt { get; private set; }

    /// <summary>Last auto-renewal result message</summary>
    public string? LastRenewalResult { get; private set; }

    /// <summary>Validity duration in days for renewal (same duration as original)</summary>
    public int ValidityDays { get; private set; } = 365;

    /// <summary>RFC 5280 revocation reason code (0=Unspecified, 1=KeyCompromise, etc.)</summary>
    public RevocationReason? RevocationReason { get; private set; }

    /// <summary>When the certificate was revoked.</summary>
    public DateTime? RevokedAt { get; private set; }

    private ManagedCertificate() { }

    public static ManagedCertificate Create(
        string commonName,
        string? subjectAltNames,
        CertType type,
        string purpose,
        string certificatePem,
        string privateKeyPem,
        string fingerprint,
        string serialNumber,
        DateTime notBefore,
        DateTime notAfter,
        string keyAlgorithm,
        int keySize,
        Guid issuingCaId,
        int renewBeforeDays = 30)
    {
        var validityDays = (int)(notAfter - notBefore).TotalDays;
        return new ManagedCertificate
        {
            CommonName = commonName,
            SubjectAltNames = subjectAltNames,
            Type = type,
            Purpose = purpose,
            CertificatePem = certificatePem,
            PrivateKeyPem = privateKeyPem,
            Fingerprint = fingerprint,
            SerialNumber = serialNumber,
            NotBefore = notBefore,
            NotAfter = notAfter,
            KeyAlgorithm = keyAlgorithm,
            KeySize = keySize,
            IssuingCaId = issuingCaId,
            RenewBeforeDays = renewBeforeDays,
            AutoRenewEnabled = true,
            ValidityDays = validityDays > 0 ? validityDays : 365,
            Status = ManagedCertStatus.Active
        };
    }

    public void Revoke(RevocationReason reason = default)
    {
        Status = ManagedCertStatus.Revoked;
        RevocationReason = reason;
        RevokedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void MarkDeployed(string target)
    {
        DeployedTo = target;
        DeployedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void MarkExpired()
    {
        Status = ManagedCertStatus.Expired;
        SetUpdated();
    }

    public bool IsExpiringSoon() => NotAfter <= DateTime.UtcNow.AddDays(RenewBeforeDays);

    public bool NeedsAutoRenewal() =>
        AutoRenewEnabled &&
        Status == ManagedCertStatus.Active &&
        ReplacedByCertId == null &&
        IsExpiringSoon();

    public void MarkSuperseded(Guid replacedByCertId)
    {
        ReplacedByCertId = replacedByCertId;
        Status = ManagedCertStatus.Superseded;
        SetUpdated();
    }

    public void SetReplacedCert(Guid replacedCertId)
    {
        ReplacedCertId = replacedCertId;
        SetUpdated();
    }

    public void RecordRenewalAttempt(string result)
    {
        LastRenewalAttempt = DateTime.UtcNow;
        LastRenewalResult = result;
        SetUpdated();
    }

    public void SetAutoRenew(bool enabled, int? renewBeforeDays = null)
    {
        AutoRenewEnabled = enabled;
        if (renewBeforeDays.HasValue) RenewBeforeDays = renewBeforeDays.Value;
        SetUpdated();
    }

    public void UpdateCertificate(string certPem, string keyPem, string fingerprint,
        string serial, DateTime notBefore, DateTime notAfter)
    {
        CertificatePem = certPem;
        PrivateKeyPem = keyPem;
        Fingerprint = fingerprint;
        SerialNumber = serial;
        NotBefore = notBefore;
        NotAfter = notAfter;
        ValidityDays = (int)(notAfter - notBefore).TotalDays;
        Status = ManagedCertStatus.Active;
        DeployedTo = null;
        DeployedAt = null;
        SetUpdated();
    }
}

public enum CertType
{
    Server = 0,
    Client = 1
}

public enum ManagedCertStatus
{
    Active = 0,
    Revoked = 1,
    Expired = 2,
    Superseded = 3
}

/// <summary>
/// RFC 5280 §5.3.1 CRL reason codes.
/// </summary>
public enum RevocationReason
{
    Unspecified = 0,
    KeyCompromise = 1,
    CaCompromise = 2,
    AffiliationChanged = 3,
    Superseded = 4,
    CessationOfOperation = 5,
    CertificateHold = 6,
    RemoveFromCrl = 8,
    PrivilegeWithdrawn = 9,
    AaCompromise = 10
}
