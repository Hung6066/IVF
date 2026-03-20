using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// FET Protocol entity for Frozen Embryo Transfer endometrial preparation tracking
/// </summary>
public class FetProtocol : BaseEntity, ITenantEntity
{
    public Guid CycleId { get; private set; }
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }

    // Preparation Type
    public string PrepType { get; private set; } = string.Empty; // Natural, HRT, MildStimulation
    public DateTime? StartDate { get; private set; }
    public int CycleDay { get; private set; }

    // Hormone Replacement (HRT protocol)
    public string? EstrogenDrug { get; private set; }
    public string? EstrogenDose { get; private set; }
    public DateTime? EstrogenStartDate { get; private set; }
    public string? ProgesteroneDrug { get; private set; }
    public string? ProgesteroneDose { get; private set; }
    public DateTime? ProgesteroneStartDate { get; private set; }

    // Endometrium monitoring
    public decimal? EndometriumThickness { get; private set; } // mm
    public string? EndometriumPattern { get; private set; } // A, B, C (triple-line)
    public DateTime? EndometriumCheckDate { get; private set; }

    // Thawing details
    public int EmbryosToThaw { get; private set; }
    public int EmbryosSurvived { get; private set; }
    public DateTime? ThawDate { get; private set; }
    public string? EmbryoGrade { get; private set; }
    public int EmbryoAge { get; private set; } // Day 3 or Day 5

    // Transfer scheduling
    public DateTime? PlannedTransferDate { get; private set; }

    public string? Notes { get; private set; }
    public string Status { get; private set; } = "Preparation"; // Preparation, ReadyForTransfer, Transferred, Cancelled

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;

    private FetProtocol() { }

    public static FetProtocol Create(
        Guid cycleId,
        string prepType,
        DateTime? startDate = null,
        int cycleDay = 1,
        string? notes = null)
    {
        return new FetProtocol
        {
            CycleId = cycleId,
            PrepType = prepType,
            StartDate = startDate,
            CycleDay = cycleDay,
            Notes = notes
        };
    }

    public void UpdateHormoneTherapy(
        string? estrogenDrug, string? estrogenDose, DateTime? estrogenStartDate,
        string? progesteroneDrug, string? progesteroneDose, DateTime? progesteroneStartDate)
    {
        EstrogenDrug = estrogenDrug;
        EstrogenDose = estrogenDose;
        EstrogenStartDate = estrogenStartDate;
        ProgesteroneDrug = progesteroneDrug;
        ProgesteroneDose = progesteroneDose;
        ProgesteroneStartDate = progesteroneStartDate;
        SetUpdated();
    }

    public void RecordEndometriumCheck(decimal thickness, string? pattern, DateTime checkDate)
    {
        EndometriumThickness = thickness;
        EndometriumPattern = pattern;
        EndometriumCheckDate = checkDate;
        SetUpdated();
    }

    public void RecordThawing(int toThaw, int survived, DateTime thawDate, string? grade, int embryoAge)
    {
        EmbryosToThaw = toThaw;
        EmbryosSurvived = survived;
        ThawDate = thawDate;
        EmbryoGrade = grade;
        EmbryoAge = embryoAge;
        Status = "ReadyForTransfer";
        SetUpdated();
    }

    public void ScheduleTransfer(DateTime transferDate)
    {
        PlannedTransferDate = transferDate;
        SetUpdated();
    }

    public void MarkTransferred()
    {
        Status = "Transferred";
        SetUpdated();
    }

    public void Cancel(string? reason = null)
    {
        Status = "Cancelled";
        if (!string.IsNullOrEmpty(reason))
            Notes = string.IsNullOrEmpty(Notes) ? reason : $"{Notes}\nHủy: {reason}";
        SetUpdated();
    }
}
