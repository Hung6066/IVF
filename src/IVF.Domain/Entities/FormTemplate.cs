using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Template for a dynamic form (similar to Google Forms)
/// </summary>
public class FormTemplate : BaseEntity
{
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Version { get; private set; } = "1.0";
    public bool IsPublished { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    // Navigation
    public FormCategory Category { get; private set; } = null!;
    public User CreatedByUser { get; private set; } = null!;
    public ICollection<FormField> Fields { get; private set; } = new List<FormField>();
    public ICollection<FormResponse> Responses { get; private set; } = new List<FormResponse>();
    public ICollection<ReportTemplate> ReportTemplates { get; private set; } = new List<ReportTemplate>();

    private FormTemplate() { }

    public static FormTemplate Create(
        Guid categoryId,
        string name,
        Guid? createdByUserId,
        string? description = null)
    {
        return new FormTemplate
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            Name = name,
            Description = description,
            Version = "1.0",
            IsPublished = false,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
        SetUpdated();
    }

    public void UpdateCategory(Guid categoryId)
    {
        CategoryId = categoryId;
        SetUpdated();
    }

    public void Publish()
    {
        IsPublished = true;
        SetUpdated();
    }

    public void Unpublish()
    {
        IsPublished = false;
        SetUpdated();
    }

    public void IncrementVersion()
    {
        var parts = Version.Split('.');
        if (parts.Length == 2 && int.TryParse(parts[1], out var minor))
        {
            Version = $"{parts[0]}.{minor + 1}";
        }
        else
        {
            Version = "1.1";
        }
        SetUpdated();
    }

    public FormField AddField(
        string fieldKey,
        string label,
        Enums.FieldType fieldType,
        int displayOrder,
        bool isRequired = false,
        string? placeholder = null,
        string? optionsJson = null,
        string? validationRulesJson = null,
        string? defaultValue = null,
        string? helpText = null,
        string? conditionalLogicJson = null,
        string? layoutJson = null)
    {
        var field = FormField.Create(
            Id,
            fieldKey,
            label,
            fieldType,
            displayOrder,
            isRequired,
            placeholder,
            optionsJson,
            validationRulesJson,
            defaultValue,
            helpText,
            conditionalLogicJson,
            layoutJson);

        Fields.Add(field);
        SetUpdated();
        return field;
    }
}
