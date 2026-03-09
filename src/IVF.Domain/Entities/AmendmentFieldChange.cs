using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Tracks an individual field change within an amendment request.
/// Stores both old and new values for diff display and rollback capability.
/// </summary>
public class AmendmentFieldChange : BaseEntity
{
    /// <summary>FK to the parent amendment.</summary>
    public Guid AmendmentId { get; private set; }

    /// <summary>FK to the FormField that was changed.</summary>
    public Guid FormFieldId { get; private set; }

    /// <summary>Field key for display/lookup.</summary>
    public string FieldKey { get; private set; } = string.Empty;

    /// <summary>Field label (Vietnamese) for display in diff view.</summary>
    public string FieldLabel { get; private set; } = string.Empty;

    /// <summary>Type of change (Modified, Added, Removed).</summary>
    public FieldChangeType ChangeType { get; private set; }

    // Old values (before amendment)
    public string? OldTextValue { get; private set; }
    public decimal? OldNumericValue { get; private set; }
    public DateTime? OldDateValue { get; private set; }
    public bool? OldBooleanValue { get; private set; }
    public string? OldJsonValue { get; private set; }

    // New values (after amendment)
    public string? NewTextValue { get; private set; }
    public decimal? NewNumericValue { get; private set; }
    public DateTime? NewDateValue { get; private set; }
    public bool? NewBooleanValue { get; private set; }
    public string? NewJsonValue { get; private set; }

    // Navigation
    public SignedDocumentAmendment Amendment { get; private set; } = null!;

    private AmendmentFieldChange() { }

    public static AmendmentFieldChange Create(
        Guid amendmentId,
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
        return new AmendmentFieldChange
        {
            Id = Guid.NewGuid(),
            AmendmentId = amendmentId,
            FormFieldId = formFieldId,
            FieldKey = fieldKey,
            FieldLabel = fieldLabel,
            ChangeType = changeType,
            OldTextValue = oldTextValue,
            NewTextValue = newTextValue,
            OldNumericValue = oldNumericValue,
            NewNumericValue = newNumericValue,
            OldDateValue = oldDateValue,
            NewDateValue = newDateValue,
            OldBooleanValue = oldBooleanValue,
            NewBooleanValue = newBooleanValue,
            OldJsonValue = oldJsonValue,
            NewJsonValue = newJsonValue,
            CreatedAt = DateTime.UtcNow
        };
    }
}
