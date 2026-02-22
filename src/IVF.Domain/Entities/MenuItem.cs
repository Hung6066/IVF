using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Represents a navigation menu item that can be configured from the admin UI.
/// Supports sections (groups) and permission-based visibility.
/// </summary>
public class MenuItem : BaseEntity
{
    /// <summary>Section/group this item belongs to (null = main menu, "admin" = quản trị, etc.)</summary>
    public string? Section { get; private set; }

    /// <summary>Section display header (e.g. "Quản trị"). Only used on the first item of a section.</summary>
    public string? SectionHeader { get; private set; }

    /// <summary>Emoji or icon identifier</summary>
    public string Icon { get; private set; } = string.Empty;

    /// <summary>Display label</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>Angular route path (e.g. "/dashboard", "/admin/users")</summary>
    public string Route { get; private set; } = string.Empty;

    /// <summary>Permission string required to see this item (e.g. "ViewPatients"). Null = always visible.</summary>
    public string? Permission { get; private set; }

    /// <summary>If true, only users with Admin role can see this item.</summary>
    public bool AdminOnly { get; private set; }

    /// <summary>Display order within the section (lower = higher).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Whether this menu item is active/visible.</summary>
    public bool IsActive { get; private set; } = true;

    // EF private constructor
    private MenuItem() { }

    public static MenuItem Create(
        string? section,
        string? sectionHeader,
        string icon,
        string label,
        string route,
        string? permission,
        bool adminOnly,
        int sortOrder,
        bool isActive = true)
    {
        return new MenuItem
        {
            Section = section,
            SectionHeader = sectionHeader,
            Icon = icon,
            Label = label,
            Route = route,
            Permission = permission,
            AdminOnly = adminOnly,
            SortOrder = sortOrder,
            IsActive = isActive
        };
    }

    public void Update(
        string? section,
        string? sectionHeader,
        string icon,
        string label,
        string route,
        string? permission,
        bool adminOnly,
        int sortOrder,
        bool isActive)
    {
        Section = section;
        SectionHeader = sectionHeader;
        Icon = icon;
        Label = label;
        Route = route;
        Permission = permission;
        AdminOnly = adminOnly;
        SortOrder = sortOrder;
        IsActive = isActive;
        SetUpdated();
    }

    public void Activate() { IsActive = true; SetUpdated(); }
    public void Deactivate() { IsActive = false; SetUpdated(); }
    public void SetOrder(int order) { SortOrder = order; SetUpdated(); }
}
