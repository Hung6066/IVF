using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class Prescription : BaseEntity, ITenantEntity
{
    public Guid PatientId { get; private set; }
    public Guid? CycleId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }
    public DateTime PrescriptionDate { get; private set; }
    public string Status { get; private set; } = "Pending"; // Pending, Entered, Printed, Dispensed, Cancelled
    public DateTime? EnteredAt { get; private set; }
    public Guid? EnteredByUserId { get; private set; }
    public DateTime? PrintedAt { get; private set; }
    public DateTime? DispensedAt { get; private set; }
    public Guid? DispensedByUserId { get; private set; }
    public string? Notes { get; private set; }
    public Guid? TemplateId { get; private set; }
    public bool WaiveConsultationFee { get; private set; }

    // Navigation properties
    public virtual Patient Patient { get; private set; } = null!;
    public virtual TreatmentCycle? Cycle { get; private set; }
    public virtual User Doctor { get; private set; } = null!;
    public virtual ICollection<PrescriptionItem> Items { get; private set; } = new List<PrescriptionItem>();

    private Prescription() { }

    public static Prescription Create(
        Guid patientId,
        Guid doctorId,
        DateTime prescriptionDate,
        Guid? cycleId = null,
        string? notes = null,
        Guid? templateId = null,
        bool waiveConsultationFee = false)
    {
        return new Prescription
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = prescriptionDate,
            CycleId = cycleId,
            Notes = notes,
            TemplateId = templateId,
            WaiveConsultationFee = waiveConsultationFee
        };
    }

    public void Enter(Guid enteredByUserId)
    {
        Status = "Entered";
        EnteredAt = DateTime.UtcNow;
        EnteredByUserId = enteredByUserId;
        SetUpdated();
    }

    public void MarkPrinted()
    {
        Status = "Printed";
        PrintedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Dispense(Guid dispensedByUserId)
    {
        Status = "Dispensed";
        DispensedAt = DateTime.UtcNow;
        DispensedByUserId = dispensedByUserId;
        SetUpdated();
    }

    public void Cancel()
    {
        Status = "Cancelled";
        SetUpdated();
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        SetUpdated();
    }

    public void AddItem(PrescriptionItem item)
    {
        Items.Add(item);
        SetUpdated();
    }
}

public class PrescriptionItem : BaseEntity
{
    public Guid PrescriptionId { get; private set; }
    public string? DrugCode { get; private set; }
    public string DrugName { get; private set; } = string.Empty;
    public string? Dosage { get; private set; }
    public string? Frequency { get; private set; }
    public string? Duration { get; private set; }
    public int Quantity { get; private set; }

    // Navigation
    public virtual Prescription Prescription { get; private set; } = null!;

    private PrescriptionItem() { }

    public static PrescriptionItem Create(
        Guid prescriptionId,
        string drugName,
        int quantity,
        string? drugCode = null,
        string? dosage = null,
        string? frequency = null,
        string? duration = null)
    {
        return new PrescriptionItem
        {
            PrescriptionId = prescriptionId,
            DrugName = drugName,
            Quantity = quantity,
            DrugCode = drugCode,
            Dosage = dosage,
            Frequency = frequency,
            Duration = duration
        };
    }
}
