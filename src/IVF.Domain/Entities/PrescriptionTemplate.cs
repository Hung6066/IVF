using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public enum PrescriptionCycleType
{
    IVF,
    IUI,
    FET,
    General
}

public class PrescriptionTemplate : BaseEntity, ITenantEntity
{
    private PrescriptionTemplate() { }

    public static PrescriptionTemplate Create(
        Guid tenantId,
        string name,
        PrescriptionCycleType cycleType,
        Guid createdByDoctorId,
        string? description) => new()
        {
            TenantId = tenantId,
            Name = name,
            CycleType = cycleType,
            CreatedByDoctorId = createdByDoctorId,
            Description = description,
            IsActive = true,
            Items = new List<PrescriptionTemplateItem>()
        };

    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }
    public string Name { get; private set; } = string.Empty;
    public PrescriptionCycleType CycleType { get; private set; }
    public Guid CreatedByDoctorId { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    public virtual Doctor? CreatedByDoctor { get; private set; }
    public virtual ICollection<PrescriptionTemplateItem> Items { get; private set; } = new List<PrescriptionTemplateItem>();

    public void Update(string name, PrescriptionCycleType cycleType, string? description)
    {
        Name = name;
        CycleType = cycleType;
        Description = description;
        SetUpdated();
    }

    public void SetItems(IEnumerable<PrescriptionTemplateItem> items)
    {
        Items = items.ToList();
        SetUpdated();
    }

    public void Activate() { IsActive = true; SetUpdated(); }
    public void Deactivate() { IsActive = false; SetUpdated(); }
}

public class PrescriptionTemplateItem : BaseEntity
{
    private PrescriptionTemplateItem() { }

    public static PrescriptionTemplateItem Create(
        Guid templateId,
        string medicationName,
        string dosage,
        string unit,
        string route,
        string frequency,
        int durationDays,
        string? instructions) => new()
        {
            TemplateId = templateId,
            MedicationName = medicationName,
            Dosage = dosage,
            Unit = unit,
            Route = route,
            Frequency = frequency,
            DurationDays = durationDays,
            Instructions = instructions
        };

    public Guid TemplateId { get; private set; }
    public string MedicationName { get; private set; } = string.Empty;
    public string Dosage { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public string Route { get; private set; } = string.Empty;
    public string Frequency { get; private set; } = string.Empty;
    public int DurationDays { get; private set; }
    public string? Instructions { get; private set; }

    public virtual PrescriptionTemplate? Template { get; private set; }
}
