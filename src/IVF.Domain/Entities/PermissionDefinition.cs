using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Database-driven permission definition. Stores permission metadata
/// (code, display name, group, icon) that can be managed from admin UI.
/// The Permission enum is kept for backend type-safety; this table is
/// the source of truth for UI grouping and display.
/// </summary>
public class PermissionDefinition : BaseEntity
{
    /// <summary>Machine-readable code matching the Permission enum name (e.g. "ViewPatients").</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Human-readable label (e.g. "Xem bệnh nhân").</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Logical group key for grouping permissions (e.g. "patient", "billing").</summary>
    public string GroupCode { get; private set; } = string.Empty;

    /// <summary>Display name for the group (e.g. "Bệnh nhân", "Hoá đơn").</summary>
    public string GroupDisplayName { get; private set; } = string.Empty;

    /// <summary>Emoji or icon class for the group.</summary>
    public string GroupIcon { get; private set; } = string.Empty;

    /// <summary>Sort order within the group.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Sort order of the group itself for display ordering.</summary>
    public int GroupSortOrder { get; private set; }

    /// <summary>Whether this permission is active and visible in the UI.</summary>
    public bool IsActive { get; private set; } = true;

    private PermissionDefinition() { }

    public static PermissionDefinition Create(
        string code,
        string displayName,
        string groupCode,
        string groupDisplayName,
        string groupIcon,
        int sortOrder,
        int groupSortOrder)
    {
        return new PermissionDefinition
        {
            Code = code,
            DisplayName = displayName,
            GroupCode = groupCode,
            GroupDisplayName = groupDisplayName,
            GroupIcon = groupIcon,
            SortOrder = sortOrder,
            GroupSortOrder = groupSortOrder,
            IsActive = true
        };
    }

    public void Update(
        string displayName,
        string groupCode,
        string groupDisplayName,
        string groupIcon,
        int sortOrder,
        int groupSortOrder)
    {
        DisplayName = displayName;
        GroupCode = groupCode;
        GroupDisplayName = groupDisplayName;
        GroupIcon = groupIcon;
        SortOrder = sortOrder;
        GroupSortOrder = groupSortOrder;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }
}
