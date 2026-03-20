using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Clinical procedure entity for tracking surgeries and procedures
/// (hysteroscopy, laparoscopy, TESA/MESA, egg retrieval, embryo transfer, etc.)
/// </summary>
public class Procedure : BaseEntity, ITenantEntity
{
    public Guid PatientId { get; private set; }
    public Guid? CycleId { get; private set; }
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }

    public Guid PerformedByDoctorId { get; private set; }
    public Guid? AssistantDoctorId { get; private set; }

    public string ProcedureType { get; private set; } = string.Empty; // EggRetrieval, EmbryoTransfer, Hysteroscopy, Laparoscopy, TESA, MESA, IUI, etc.
    public string? ProcedureCode { get; private set; }
    public string ProcedureName { get; private set; } = string.Empty;

    public DateTime ScheduledAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int? DurationMinutes { get; private set; }

    public string? AnesthesiaType { get; private set; } // None, Local, General, Sedation
    public string? AnesthesiaNotes { get; private set; }
    public string? RoomNumber { get; private set; }

    // Clinical findings
    public string? PreOpNotes { get; private set; }
    public string? IntraOpFindings { get; private set; }
    public string? PostOpNotes { get; private set; }
    public string? Complications { get; private set; }

    public string Status { get; private set; } = "Scheduled"; // Scheduled, InProgress, Completed, Cancelled, Postponed

    // Navigation
    public virtual Patient Patient { get; private set; } = null!;
    public virtual TreatmentCycle? Cycle { get; private set; }
    public virtual Doctor PerformedByDoctor { get; private set; } = null!;
    public virtual Doctor? AssistantDoctor { get; private set; }

    private Procedure() { }

    public static Procedure Create(
        Guid patientId,
        Guid performedByDoctorId,
        string procedureType,
        string procedureName,
        DateTime scheduledAt,
        Guid? cycleId = null,
        Guid? assistantDoctorId = null,
        string? procedureCode = null,
        string? anesthesiaType = null,
        string? roomNumber = null,
        string? preOpNotes = null)
    {
        return new Procedure
        {
            PatientId = patientId,
            PerformedByDoctorId = performedByDoctorId,
            ProcedureType = procedureType,
            ProcedureName = procedureName,
            ScheduledAt = scheduledAt,
            CycleId = cycleId,
            AssistantDoctorId = assistantDoctorId,
            ProcedureCode = procedureCode,
            AnesthesiaType = anesthesiaType,
            RoomNumber = roomNumber,
            PreOpNotes = preOpNotes
        };
    }

    public void Start()
    {
        StartedAt = DateTime.UtcNow;
        Status = "InProgress";
        SetUpdated();
    }

    public void Complete(string? intraOpFindings, string? postOpNotes, string? complications, int? durationMinutes)
    {
        CompletedAt = DateTime.UtcNow;
        IntraOpFindings = intraOpFindings;
        PostOpNotes = postOpNotes;
        Complications = complications;
        DurationMinutes = durationMinutes;
        Status = "Completed";
        SetUpdated();
    }

    public void Cancel(string? reason = null)
    {
        Status = "Cancelled";
        PostOpNotes = string.IsNullOrEmpty(PostOpNotes) ? reason : $"{PostOpNotes}\nHủy: {reason}";
        SetUpdated();
    }

    public void Postpone(DateTime newScheduledAt, string? reason = null)
    {
        ScheduledAt = newScheduledAt;
        Status = "Postponed";
        if (!string.IsNullOrEmpty(reason))
            PreOpNotes = string.IsNullOrEmpty(PreOpNotes) ? $"Hoãn: {reason}" : $"{PreOpNotes}\nHoãn: {reason}";
        SetUpdated();
    }

    public void UpdateAnesthesia(string? type, string? notes)
    {
        AnesthesiaType = type;
        AnesthesiaNotes = notes;
        SetUpdated();
    }
}
