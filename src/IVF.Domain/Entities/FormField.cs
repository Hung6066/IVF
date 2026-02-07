using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Individual field definition within a form template
/// </summary>
public class FormField : BaseEntity
{
    public Guid FormTemplateId { get; private set; }
    public string FieldKey { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string? Placeholder { get; private set; }
    public FieldType FieldType { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsRequired { get; private set; }
    public string? ValidationRulesJson { get; private set; }
    public string? OptionsJson { get; private set; }
    public string? DefaultValue { get; private set; }
    public string? HelpText { get; private set; }
    public string? ConditionalLogicJson { get; private set; }

    // Link to medical concept
    public Guid? ConceptId { get; private set; }

    // Navigation
    public FormTemplate FormTemplate { get; private set; } = null!;
    public Concept? Concept { get; private set; }
    public ICollection<FormFieldValue> FieldValues { get; private set; } = new List<FormFieldValue>();
    public ICollection<FormFieldOption> Options { get; private set; } = new List<FormFieldOption>();

    private FormField() { }

    public static FormField Create(
        Guid formTemplateId,
        string fieldKey,
        string label,
        FieldType fieldType,
        int displayOrder,
        bool isRequired = false,
        string? placeholder = null,
        string? optionsJson = null,
        string? validationRulesJson = null,
        string? defaultValue = null,
        string? helpText = null,
        string? conditionalLogicJson = null)
    {
        return new FormField
        {
            Id = Guid.NewGuid(),
            FormTemplateId = formTemplateId,
            FieldKey = fieldKey,
            Label = label,
            FieldType = fieldType,
            DisplayOrder = displayOrder,
            IsRequired = isRequired,
            Placeholder = placeholder,
            OptionsJson = optionsJson,
            ValidationRulesJson = validationRulesJson,
            DefaultValue = defaultValue,
            HelpText = helpText,
            ConditionalLogicJson = conditionalLogicJson,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string label,
        string? placeholder,
        FieldType fieldType,
        int displayOrder,
        bool isRequired,
        string? optionsJson,
        string? validationRulesJson,
        string? defaultValue,
        string? helpText,
        string? conditionalLogicJson)
    {
        Label = label;
        Placeholder = placeholder;
        FieldType = fieldType;
        DisplayOrder = displayOrder;
        IsRequired = isRequired;
        OptionsJson = optionsJson;
        ValidationRulesJson = validationRulesJson;
        DefaultValue = defaultValue;
        HelpText = helpText;
        ConditionalLogicJson = conditionalLogicJson;
        SetUpdated();
    }

    public void UpdateOrder(int displayOrder)
    {
        DisplayOrder = displayOrder;
        SetUpdated();
    }

    /// <summary>
    /// Link this field to a medical concept from your concept library
    /// </summary>
    public void LinkToConcept(Guid conceptId)
    {
        ConceptId = conceptId;
        SetUpdated();
    }

    /// <summary>
    /// Remove concept mapping from this field
    /// </summary>
    public void UnlinkConcept()
    {
        ConceptId = null;
        SetUpdated();
    }

    /// <summary>
    /// Add an option to this field (for dropdown/radio/checkbox types)
    /// </summary>
    public FormFieldOption AddOption(
        string value,
        string label,
        int displayOrder,
        Guid? conceptId = null)
    {
        var option = FormFieldOption.Create(
            this.Id,
            value,
            label,
            displayOrder,
            conceptId
        );
        
        Options.Add(option);
        SetUpdated();
        return option;
    }
}
