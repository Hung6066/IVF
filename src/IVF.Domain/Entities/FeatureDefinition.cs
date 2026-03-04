using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Defines a platform feature that can be assigned to plans and controls menu/functionality visibility.
/// Examples: "ai", "digital_signing", "biometrics", "advanced_reporting", "patient_management", etc.
/// </summary>
public class FeatureDefinition : BaseEntity
{
    /// <summary>Machine-readable unique code (e.g. "ai", "digital_signing", "biometrics").</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Human-readable display name (e.g. "AI hỗ trợ chẩn đoán").</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Description shown on pricing page.</summary>
    public string? Description { get; private set; }

    /// <summary>Emoji or icon identifier.</summary>
    public string Icon { get; private set; } = string.Empty;

    /// <summary>Category for grouping (e.g. "core", "advanced", "enterprise").</summary>
    public string Category { get; private set; } = "core";

    /// <summary>Display order within category.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Whether this feature is active.</summary>
    public bool IsActive { get; private set; } = true;

    private FeatureDefinition() { }

    public static FeatureDefinition Create(
        string code,
        string displayName,
        string? description,
        string icon,
        string category,
        int sortOrder)
    {
        return new FeatureDefinition
        {
            Code = code,
            DisplayName = displayName,
            Description = description,
            Icon = icon,
            Category = category,
            SortOrder = sortOrder
        };
    }

    public void Update(string displayName, string? description, string icon, string category, int sortOrder, bool isActive)
    {
        DisplayName = displayName;
        Description = description;
        Icon = icon;
        Category = category;
        SortOrder = sortOrder;
        IsActive = isActive;
        SetUpdated();
    }
}
