using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// User group/team for organizational hierarchy — inspired by Google Workspace Groups,
/// AWS IAM Groups, and Facebook Workplace Teams.
/// Groups allow bulk permission assignment and organizational structuring.
/// </summary>
public class UserGroup : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public string? Description { get; private set; }
    public string GroupType { get; private set; } = "team"; // team, department, role-group, custom
    public Guid? ParentGroupId { get; private set; }
    public bool IsSystem { get; private set; } // System groups cannot be deleted
    public bool IsActive { get; private set; } = true;
    public string? Metadata { get; private set; } // JSON for extensible properties

    private UserGroup() { }

    public static UserGroup Create(
        string name,
        string? displayName = null,
        string? description = null,
        string groupType = "team",
        Guid? parentGroupId = null,
        bool isSystem = false)
    {
        return new UserGroup
        {
            Name = name,
            DisplayName = displayName ?? name,
            Description = description,
            GroupType = groupType,
            ParentGroupId = parentGroupId,
            IsSystem = isSystem,
            IsActive = true
        };
    }

    public void Update(string name, string? displayName, string? description, string groupType)
    {
        Name = name;
        DisplayName = displayName ?? name;
        Description = description;
        GroupType = groupType;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }

    public void SetMetadata(string metadata)
    {
        Metadata = metadata;
        SetUpdated();
    }
}
