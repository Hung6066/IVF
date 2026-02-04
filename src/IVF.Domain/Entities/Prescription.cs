using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class Prescription : BaseEntity
{
    public Guid PatientId { get; private set; }
    public Guid? CycleId { get; private set; }
    public Guid DoctorId { get; private set; }
    public DateTime PrescriptionDate { get; private set; }
    public string Status { get; private set; } = "Pending"; // Pending, Dispensed, Cancelled
    public DateTime? DispensedAt { get; private set; }
    public string? Notes { get; private set; }

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
        string? notes = null)
    {
        return new Prescription
        {
            PatientId = patientId,
            DoctorId = doctorId,
            PrescriptionDate = prescriptionDate,
            CycleId = cycleId,
            Notes = notes
        };
    }

    public void Dispense()
    {
        Status = "Dispensed";
        DispensedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Cancel()
    {
        Status = "Cancelled";
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
