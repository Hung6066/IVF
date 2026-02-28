using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Full lifecycle audit log for certificate operations.
/// Tracks create, issue, deploy, rotate, revoke, CRL generation, and OCSP queries.
/// </summary>
public class CertificateAuditEvent : BaseEntity
{
    /// <summary>Related certificate ID (nullable for CA-level events like CRL generation).</summary>
    public Guid? CertificateId { get; private set; }

    /// <summary>Related CA ID.</summary>
    public Guid? CaId { get; private set; }

    /// <summary>Type of event.</summary>
    public CertAuditEventType EventType { get; private set; }

    /// <summary>Human-readable description of what happened.</summary>
    public string Description { get; private set; } = null!;

    /// <summary>User or system identity that performed the action.</summary>
    public string Actor { get; private set; } = "system";

    /// <summary>Source IP address if applicable.</summary>
    public string? SourceIp { get; private set; }

    /// <summary>Additional metadata (JSON) — serial number, fingerprint, target, etc.</summary>
    public string? Metadata { get; private set; }

    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; private set; } = true;

    /// <summary>Error message if the operation failed.</summary>
    public string? ErrorMessage { get; private set; }

    private CertificateAuditEvent() { }

    public static CertificateAuditEvent Create(
        CertAuditEventType eventType,
        string description,
        string actor = "system",
        Guid? certificateId = null,
        Guid? caId = null,
        string? sourceIp = null,
        string? metadata = null,
        bool success = true,
        string? errorMessage = null)
    {
        return new CertificateAuditEvent
        {
            EventType = eventType,
            Description = description,
            Actor = actor,
            CertificateId = certificateId,
            CaId = caId,
            SourceIp = sourceIp,
            Metadata = metadata,
            Success = success,
            ErrorMessage = errorMessage
        };
    }
}

public enum CertAuditEventType
{
    CaCreated = 0,
    CaRevoked = 1,
    CertIssued = 10,
    CertRenewed = 11,
    CertRevoked = 12,
    CertExpired = 13,
    CertSuperseded = 14,
    CertDeployed = 20,
    CertDeployFailed = 21,
    AutoRenewTriggered = 30,
    AutoRenewFailed = 31,
    CrlGenerated = 40,
    OcspQuery = 50,
    IntermediateCaCreated = 60,
    CertRotationStarted = 70,
    CertRotationCompleted = 71
}
