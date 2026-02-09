using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Detailed value element for multi-value fields (e.g. Tags, MultiSelect)
/// Allows normalized reporting on individual selected options or tags
/// </summary>
public class FormFieldValueDetail : BaseEntity
{
    public Guid FormFieldValueId { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public string? Label { get; private set; } // Display text at time of saving
    public Guid? ConceptId { get; private set; }

    // Navigation
    public FormFieldValue FormFieldValue { get; private set; } = null!;
    public Concept? Concept { get; private set; }

    private FormFieldValueDetail() { }

    public static FormFieldValueDetail Create(
        Guid formFieldValueId,
        string value,
        string? label = null,
        Guid? conceptId = null)
    {
        return new FormFieldValueDetail
        {
            Id = Guid.NewGuid(),
            FormFieldValueId = formFieldValueId,
            Value = value,
            Label = label ?? value,
            ConceptId = conceptId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
