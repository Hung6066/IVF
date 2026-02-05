using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Service catalog for patient indications and invoice pricing
/// </summary>
public class ServiceCatalog : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public ServiceCategory Category { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Unit { get; private set; } = "lần"; // lần, viên, ml, etc.
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ServiceCatalog() { }

    public static ServiceCatalog Create(
        string code,
        string name,
        ServiceCategory category,
        decimal unitPrice,
        string unit = "lần",
        string? description = null)
    {
        return new ServiceCatalog
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Category = category,
            UnitPrice = unitPrice,
            Unit = unit,
            Description = description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, ServiceCategory category, decimal unitPrice, string unit, string? description)
    {
        Name = name;
        Category = category;
        UnitPrice = unitPrice;
        Unit = unit;
        Description = description;
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
