using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Database-driven subscription plan definition. Replaces hardcoded plan pricing/limits dictionaries.
/// </summary>
public class PlanDefinition : BaseEntity
{
    /// <summary>Maps to SubscriptionPlan enum.</summary>
    public SubscriptionPlan Plan { get; private set; }

    /// <summary>Display name (e.g. "Professional").</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Short description for pricing page.</summary>
    public string? Description { get; private set; }

    /// <summary>Monthly price in base currency.</summary>
    public decimal MonthlyPrice { get; private set; }

    /// <summary>Currency code (e.g. "VND").</summary>
    public string Currency { get; private set; } = "VND";

    /// <summary>Duration display text (e.g. "30 ngày", "Tháng").</summary>
    public string Duration { get; private set; } = "Tháng";

    /// <summary>Max number of users allowed.</summary>
    public int MaxUsers { get; private set; }

    /// <summary>Max patients per month.</summary>
    public int MaxPatientsPerMonth { get; private set; }

    /// <summary>Storage limit in MB.</summary>
    public long StorageLimitMb { get; private set; }

    /// <summary>Display order on pricing page.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Whether this plan is featured/highlighted on pricing page.</summary>
    public bool IsFeatured { get; private set; }

    /// <summary>Whether this plan is active and available for selection.</summary>
    public bool IsActive { get; private set; } = true;

    // Navigation
    public ICollection<PlanFeature> PlanFeatures { get; private set; } = new List<PlanFeature>();

    private PlanDefinition() { }

    public static PlanDefinition Create(
        SubscriptionPlan plan,
        string displayName,
        string? description,
        decimal monthlyPrice,
        string currency,
        string duration,
        int maxUsers,
        int maxPatientsPerMonth,
        long storageLimitMb,
        int sortOrder,
        bool isFeatured = false)
    {
        return new PlanDefinition
        {
            Plan = plan,
            DisplayName = displayName,
            Description = description,
            MonthlyPrice = monthlyPrice,
            Currency = currency,
            Duration = duration,
            MaxUsers = maxUsers,
            MaxPatientsPerMonth = maxPatientsPerMonth,
            StorageLimitMb = storageLimitMb,
            SortOrder = sortOrder,
            IsFeatured = isFeatured
        };
    }

    public void Update(
        string displayName,
        string? description,
        decimal monthlyPrice,
        string duration,
        int maxUsers,
        int maxPatientsPerMonth,
        long storageLimitMb,
        int sortOrder,
        bool isFeatured,
        bool isActive)
    {
        DisplayName = displayName;
        Description = description;
        MonthlyPrice = monthlyPrice;
        Duration = duration;
        MaxUsers = maxUsers;
        MaxPatientsPerMonth = maxPatientsPerMonth;
        StorageLimitMb = storageLimitMb;
        SortOrder = sortOrder;
        IsFeatured = isFeatured;
        IsActive = isActive;
        SetUpdated();
    }
}
