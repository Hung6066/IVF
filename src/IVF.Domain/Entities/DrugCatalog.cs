using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public enum DrugCategory
{
    Gonadotropin,
    GnRH,
    Progesterone,
    Estrogen,
    Trigger,
    Antibiotic,
    Supplement,
    Other
}

public class DrugCatalog : BaseEntity, ITenantEntity
{
    private DrugCatalog() { }

    public static DrugCatalog Create(
        Guid tenantId,
        string code,
        string name,
        string genericName,
        DrugCategory category,
        string unit,
        string? activeIngredient,
        string? defaultDosage,
        string? notes) => new()
        {
            TenantId = tenantId,
            Code = code,
            Name = name,
            GenericName = genericName,
            Category = category,
            Unit = unit,
            ActiveIngredient = activeIngredient,
            DefaultDosage = defaultDosage,
            Notes = notes,
            IsActive = true
        };

    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string GenericName { get; private set; } = string.Empty;
    public DrugCategory Category { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public string? ActiveIngredient { get; private set; }
    public string? DefaultDosage { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, string genericName, DrugCategory category, string unit,
        string? activeIngredient, string? defaultDosage, string? notes)
    {
        Name = name;
        GenericName = genericName;
        Category = category;
        Unit = unit;
        ActiveIngredient = activeIngredient;
        DefaultDosage = defaultDosage;
        Notes = notes;
        SetUpdated();
    }

    public void Activate() { IsActive = true; SetUpdated(); }
    public void Deactivate() { IsActive = false; SetUpdated(); }
}
