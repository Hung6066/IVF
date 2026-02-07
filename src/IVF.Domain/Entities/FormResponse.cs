using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// A submitted form response containing field values
/// </summary>
public class FormResponse : BaseEntity
{
    public Guid FormTemplateId { get; private set; }
    public Guid? PatientId { get; private set; }
    public Guid? CycleId { get; private set; }
    public Guid SubmittedByUserId { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public ResponseStatus Status { get; private set; }
    public string? Notes { get; private set; }

    // Navigation
    public FormTemplate FormTemplate { get; private set; } = null!;
    public Patient? Patient { get; private set; }
    public TreatmentCycle? Cycle { get; private set; }
    public User SubmittedByUser { get; private set; } = null!;
    public ICollection<FormFieldValue> FieldValues { get; private set; } = new List<FormFieldValue>();

    private FormResponse() { }

    public static FormResponse Create(
        Guid formTemplateId,
        Guid submittedByUserId,
        Guid? patientId = null,
        Guid? cycleId = null)
    {
        return new FormResponse
        {
            Id = Guid.NewGuid(),
            FormTemplateId = formTemplateId,
            PatientId = patientId,
            CycleId = cycleId,
            SubmittedByUserId = submittedByUserId,
            Status = ResponseStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Submit()
    {
        Status = ResponseStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void MarkAsReviewed()
    {
        Status = ResponseStatus.Reviewed;
        SetUpdated();
    }

    public void Approve()
    {
        Status = ResponseStatus.Approved;
        SetUpdated();
    }

    public void Reject(string? notes = null)
    {
        Status = ResponseStatus.Rejected;
        Notes = notes;
        SetUpdated();
    }

    public void AddNotes(string notes)
    {
        Notes = notes;
        SetUpdated();
    }

    public FormFieldValue AddFieldValue(
        Guid formFieldId,
        string? textValue = null,
        decimal? numericValue = null,
        DateTime? dateValue = null,
        bool? booleanValue = null,
        string? jsonValue = null)
    {
        var fieldValue = FormFieldValue.Create(
            Id,
            formFieldId,
            textValue,
            numericValue,
            dateValue,
            booleanValue,
            jsonValue);

        FieldValues.Add(fieldValue);
        SetUpdated();
        return fieldValue;
    }
}
