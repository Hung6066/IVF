using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class MedicationAdministration : BaseEntity, ITenantEntity
{
    public Guid PatientId { get; private set; }
    public Guid CycleId { get; private set; }
    public Guid? PrescriptionId { get; private set; }
    public Guid AdministeredByUserId { get; private set; }
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }

    public string MedicationName { get; private set; } = string.Empty;
    public string? MedicationCode { get; private set; }
    public string Dosage { get; private set; } = string.Empty; // e.g. "150 IU"
    public string Route { get; private set; } = string.Empty; // SC, IM, PO, IV
    public string? Site { get; private set; } // Injection site
    public DateTime AdministeredAt { get; private set; }
    public DateTime? ScheduledAt { get; private set; }

    public bool IsTriggerShot { get; private set; }
    public string? BatchNumber { get; private set; }
    public string? Notes { get; private set; }
    public string Status { get; private set; } = "Administered"; // Scheduled, Administered, Skipped, Refused

    // Navigation
    public virtual Patient Patient { get; private set; } = null!;
    public virtual TreatmentCycle Cycle { get; private set; } = null!;
    public virtual Prescription? Prescription { get; private set; }
    public virtual User AdministeredBy { get; private set; } = null!;

    private MedicationAdministration() { }

    public static MedicationAdministration Create(
        Guid patientId,
        Guid cycleId,
        Guid administeredByUserId,
        string medicationName,
        string dosage,
        string route,
        DateTime administeredAt,
        Guid? prescriptionId = null,
        string? medicationCode = null,
        string? site = null,
        DateTime? scheduledAt = null,
        bool isTriggerShot = false,
        string? batchNumber = null,
        string? notes = null)
    {
        return new MedicationAdministration
        {
            PatientId = patientId,
            CycleId = cycleId,
            AdministeredByUserId = administeredByUserId,
            MedicationName = medicationName,
            Dosage = dosage,
            Route = route,
            AdministeredAt = administeredAt,
            PrescriptionId = prescriptionId,
            MedicationCode = medicationCode,
            Site = site,
            ScheduledAt = scheduledAt,
            IsTriggerShot = isTriggerShot,
            BatchNumber = batchNumber,
            Notes = notes
        };
    }

    public void MarkSkipped(string? reason)
    {
        Status = "Skipped";
        Notes = reason;
        SetUpdated();
    }

    public void MarkRefused(string? reason)
    {
        Status = "Refused";
        Notes = reason;
        SetUpdated();
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        SetUpdated();
    }
}
