using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Represents a Certificate Authority managed by the system.
/// Supports self-signed root CA and intermediate CA for mTLS.
/// </summary>
public class CertificateAuthority : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string CommonName { get; private set; } = null!;
    public string Organization { get; private set; } = "IVF System";
    public string? OrganizationalUnit { get; private set; }
    public string Country { get; private set; } = "VN";
    public string? State { get; private set; }
    public string? Locality { get; private set; }

    /// <summary>Root or Intermediate</summary>
    public CaType Type { get; private set; } = CaType.Root;

    /// <summary>RSA key size (2048, 4096) or EC curve name</summary>
    public string KeyAlgorithm { get; private set; } = "RSA";
    public int KeySize { get; private set; } = 4096;

    /// <summary>PEM-encoded CA certificate (public)</summary>
    public string CertificatePem { get; private set; } = null!;

    /// <summary>PEM-encoded CA private key (encrypted with passphrase)</summary>
    public string PrivateKeyPem { get; private set; } = null!;

    /// <summary>SHA-256 fingerprint of the CA certificate</summary>
    public string Fingerprint { get; private set; } = null!;

    /// <summary>Serial number counter for issued certificates</summary>
    public long NextSerialNumber { get; private set; } = 1;

    /// <summary>CRL number counter (monotonically increasing per RFC 5280)</summary>
    public long NextCrlNumber { get; private set; } = 1;

    public DateTime NotBefore { get; private set; }
    public DateTime NotAfter { get; private set; }

    /// <summary>Parent CA ID for intermediate CAs</summary>
    public Guid? ParentCaId { get; private set; }
    public CertificateAuthority? ParentCa { get; private set; }

    /// <summary>Full PEM chain (this cert + parent chain)</summary>
    public string? ChainPem { get; private set; }

    public CaStatus Status { get; private set; } = CaStatus.Active;

    // Navigation
    public ICollection<ManagedCertificate> IssuedCertificates { get; private set; } = [];

    private CertificateAuthority() { }

    public static CertificateAuthority Create(
        string name,
        string commonName,
        string organization,
        string? orgUnit,
        string country,
        string? state,
        string? locality,
        CaType type,
        string keyAlgorithm,
        int keySize,
        string certificatePem,
        string privateKeyPem,
        string fingerprint,
        DateTime notBefore,
        DateTime notAfter,
        Guid? parentCaId = null,
        string? chainPem = null)
    {
        return new CertificateAuthority
        {
            Name = name,
            CommonName = commonName,
            Organization = organization,
            OrganizationalUnit = orgUnit,
            Country = country,
            State = state,
            Locality = locality,
            Type = type,
            KeyAlgorithm = keyAlgorithm,
            KeySize = keySize,
            CertificatePem = certificatePem,
            PrivateKeyPem = privateKeyPem,
            Fingerprint = fingerprint,
            NotBefore = notBefore,
            NotAfter = notAfter,
            ParentCaId = parentCaId,
            ChainPem = chainPem,
            Status = CaStatus.Active,
            NextSerialNumber = 1
        };
    }

    public long AllocateSerialNumber()
    {
        var serial = NextSerialNumber;
        NextSerialNumber++;
        SetUpdated();
        return serial;
    }

    public long AllocateCrlNumber()
    {
        var crlNum = NextCrlNumber;
        NextCrlNumber++;
        SetUpdated();
        return crlNum;
    }

    public void Revoke()
    {
        Status = CaStatus.Revoked;
        SetUpdated();
    }

    public void UpdateChain(string chainPem)
    {
        ChainPem = chainPem;
        SetUpdated();
    }
}

public enum CaType
{
    Root = 0,
    Intermediate = 1
}

public enum CaStatus
{
    Active = 0,
    Revoked = 1,
    Expired = 2
}
