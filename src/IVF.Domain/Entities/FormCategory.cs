using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Category for organizing form templates
/// </summary>
public class FormCategory : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? IconName { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Navigation
    public ICollection<FormTemplate> FormTemplates { get; private set; } = new List<FormTemplate>();

    private FormCategory() { }

    public static FormCategory Create(string name, string? description = null, string? iconName = null, int displayOrder = 0)
    {
        return new FormCategory
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            IconName = iconName,
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description, string? iconName, int displayOrder)
    {
        Name = name;
        Description = description;
        IconName = iconName;
        DisplayOrder = displayOrder;
        SetUpdated();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        SetUpdated();
    }
}
