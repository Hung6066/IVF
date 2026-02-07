using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Represents an option for form fields like dropdown, radio, or checkbox
/// Each option can be mapped to a medical concept (e.g., SNOMED CT code)
/// </summary>
public class FormFieldOption : BaseEntity
{
    public Guid FormFieldId { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }

    // Link to medical concept
    public Guid? ConceptId { get; private set; }

    // Navigation
    public FormField FormField { get; private set; } = null!;
    public Concept? Concept { get; private set; }

    private FormFieldOption() { }

    public static FormFieldOption Create(
        Guid formFieldId,
        string value,
        string label,
        int displayOrder,
        Guid? conceptId = null)
    {
        return new FormFieldOption
        {
            Id = Guid.NewGuid(),
            FormFieldId = formFieldId,
            Value = value,
            Label = label,
            DisplayOrder = displayOrder,
            ConceptId = conceptId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string value,
        string label,
        int displayOrder)
    {
        Value = value;
        Label = label;
        DisplayOrder = displayOrder;
        SetUpdated();
    }

    /// <summary>
    /// Link this option to a medical concept from your concept library
    /// </summary>
    public void LinkToConcept(Guid conceptId)
    {
        ConceptId = conceptId;
        SetUpdated();
    }

    /// <summary>
    /// Remove concept mapping
    /// </summary>
    public void UnlinkConcept()
    {
        ConceptId = null;
        SetUpdated();
    }
}
