using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// GDPR Art. 12-23 — Data Subject Request tracking.
/// Manages right of access, rectification, erasure, restriction, portability, objection.
/// </summary>
public class DataSubjectRequest : BaseEntity
{
    public string RequestReference { get; private set; } = string.Empty;
    public Guid? PatientId { get; private set; }
    public string DataSubjectName { get; private set; } = string.Empty;
    public string DataSubjectEmail { get; private set; } = string.Empty;
    public string RequestType { get; private set; } = string.Empty; // Access, Rectification, Erasure, Restriction, Portability, Objection
    public string Description { get; private set; } = string.Empty;
    public string Status { get; private set; } = DsrStatus.Received;
    public string? IdentityVerificationMethod { get; private set; }
    public bool IdentityVerified { get; private set; }
    public DateTime? IdentityVerifiedAt { get; private set; }
    public Guid? IdentityVerifiedBy { get; private set; }
    public DateTime ReceivedAt { get; private set; } = DateTime.UtcNow;
    public DateTime Deadline { get; private set; } // 30-day GDPR deadline
    public DateTime? ExtendedDeadline { get; private set; } // Up to 60 additional days for complex requests
    public string? ExtensionReason { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? AssignedTo { get; private set; }
    public string? ResponseSummary { get; private set; }
    public string? RejectionReason { get; private set; } // Only for manifestly unfounded/excessive
    public string? AttachmentPaths { get; private set; } // JSON array of MinIO paths
    public string? InternalNotes { get; private set; }
    public string? LegalBasis { get; private set; } // Legal basis for refusal if applicable
    public bool NotifiedDataSubject { get; private set; }
    public DateTime? NotifiedAt { get; private set; }
    public bool EscalatedToDpo { get; private set; }
    public DateTime? EscalatedAt { get; private set; }

    private DataSubjectRequest() { }

    public static DataSubjectRequest Create(
        string requestReference,
        string dataSubjectName,
        string dataSubjectEmail,
        string requestType,
        string description,
        Guid? patientId = null)
    {
        return new DataSubjectRequest
        {
            RequestReference = requestReference,
            DataSubjectName = dataSubjectName,
            DataSubjectEmail = dataSubjectEmail,
            RequestType = requestType,
            Description = description,
            PatientId = patientId,
            ReceivedAt = DateTime.UtcNow,
            Deadline = DateTime.UtcNow.AddDays(30),
            Status = DsrStatus.Received
        };
    }

    public void VerifyIdentity(string method, Guid verifiedBy)
    {
        IdentityVerificationMethod = method;
        IdentityVerified = true;
        IdentityVerifiedAt = DateTime.UtcNow;
        IdentityVerifiedBy = verifiedBy;
        Status = DsrStatus.IdentityVerified;
        SetUpdated();
    }

    public void AssignHandler(Guid userId)
    {
        AssignedTo = userId;
        Status = DsrStatus.InProgress;
        SetUpdated();
    }

    public void ExtendDeadline(string reason)
    {
        ExtensionReason = reason;
        ExtendedDeadline = Deadline.AddDays(60);
        SetUpdated();
    }

    public void Complete(string responseSummary)
    {
        ResponseSummary = responseSummary;
        CompletedAt = DateTime.UtcNow;
        Status = DsrStatus.Completed;
        SetUpdated();
    }

    public void Reject(string reason, string? legalBasis)
    {
        RejectionReason = reason;
        LegalBasis = legalBasis;
        Status = DsrStatus.Rejected;
        CompletedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void NotifyDataSubject()
    {
        NotifiedDataSubject = true;
        NotifiedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void EscalateToDpo()
    {
        EscalatedToDpo = true;
        EscalatedAt = DateTime.UtcNow;
        Status = DsrStatus.EscalatedToDpo;
        SetUpdated();
    }

    public void AddNote(string note)
    {
        InternalNotes = string.IsNullOrEmpty(InternalNotes)
            ? note
            : $"{InternalNotes}\n---\n{note}";
        SetUpdated();
    }

    public bool IsOverdue => DateTime.UtcNow > (ExtendedDeadline ?? Deadline)
                             && Status != DsrStatus.Completed
                             && Status != DsrStatus.Rejected;

    public int DaysRemaining => Math.Max(0, ((ExtendedDeadline ?? Deadline) - DateTime.UtcNow).Days);
}

public static class DsrStatus
{
    public const string Received = "Received";
    public const string IdentityVerified = "IdentityVerified";
    public const string InProgress = "InProgress";
    public const string EscalatedToDpo = "EscalatedToDpo";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";
}

public static class DsrType
{
    public const string Access = "Access";
    public const string Rectification = "Rectification";
    public const string Erasure = "Erasure";
    public const string Restriction = "Restriction";
    public const string Portability = "Portability";
    public const string Objection = "Objection";
}
