using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Tracks an amendment request for a signed form response.
/// After a form response is digitally signed, users can request amendments
/// which must be approved before the data is updated and re-signed.
/// Each amendment creates a version for full audit trail.
/// </summary>
public class SignedDocumentAmendment : BaseEntity, ITenantEntity
{
    /// <summary>FK to the FormResponse being amended.</summary>
    public Guid FormResponseId { get; private set; }

    /// <summary>FK to the User who requested the amendment.</summary>
    public Guid RequestedByUserId { get; private set; }

    /// <summary>FK to the User who approved/rejected the amendment.</summary>
    public Guid? ReviewedByUserId { get; private set; }

    /// <summary>Auto-incremented version number per FormResponse.</summary>
    public int Version { get; private set; }

    /// <summary>Current status of the amendment request.</summary>
    public AmendmentStatus Status { get; private set; }

    /// <summary>Reason for requesting the amendment (required).</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Notes from the reviewer when approving/rejecting.</summary>
    public string? ReviewNotes { get; private set; }

    /// <summary>When the amendment was reviewed.</summary>
    public DateTime? ReviewedAt { get; private set; }

    /// <summary>JSON snapshot of all old field values before amendment (for full audit).</summary>
    public string? OldValuesSnapshot { get; private set; }

    /// <summary>JSON snapshot of all new field values after amendment.</summary>
    public string? NewValuesSnapshot { get; private set; }

    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }

    // Navigation
    public User RequestedByUser { get; private set; } = null!;
    public User? ReviewedByUser { get; private set; }
    public virtual ICollection<AmendmentFieldChange> FieldChanges { get; private set; } = new List<AmendmentFieldChange>();

    private SignedDocumentAmendment() { }

    public static SignedDocumentAmendment Create(
        Guid formResponseId,
        Guid requestedByUserId,
        int version,
        string reason,
        string? oldValuesSnapshot = null,
        string? newValuesSnapshot = null)
    {
        return new SignedDocumentAmendment
        {
            Id = Guid.NewGuid(),
            FormResponseId = formResponseId,
            RequestedByUserId = requestedByUserId,
            Version = version,
            Status = AmendmentStatus.Pending,
            Reason = reason,
            OldValuesSnapshot = oldValuesSnapshot,
            NewValuesSnapshot = newValuesSnapshot,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve(Guid reviewedByUserId, string? notes = null)
    {
        Status = AmendmentStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ReviewNotes = notes;
        ReviewedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Reject(Guid reviewedByUserId, string? notes = null)
    {
        Status = AmendmentStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        ReviewNotes = notes;
        ReviewedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public AmendmentFieldChange AddFieldChange(
        Guid formFieldId,
        string fieldKey,
        string fieldLabel,
        FieldChangeType changeType,
        string? oldTextValue = null,
        string? newTextValue = null,
        decimal? oldNumericValue = null,
        decimal? newNumericValue = null,
        DateTime? oldDateValue = null,
        DateTime? newDateValue = null,
        bool? oldBooleanValue = null,
        bool? newBooleanValue = null,
        string? oldJsonValue = null,
        string? newJsonValue = null)
    {
        var change = AmendmentFieldChange.Create(
            Id, formFieldId, fieldKey, fieldLabel, changeType,
            oldTextValue, newTextValue,
            oldNumericValue, newNumericValue,
            oldDateValue, newDateValue,
            oldBooleanValue, newBooleanValue,
            oldJsonValue, newJsonValue);

        FieldChanges.Add(change);
        return change;
    }
}
