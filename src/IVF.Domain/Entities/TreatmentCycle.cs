using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class TreatmentCycle : BaseEntity
{
    public Guid CoupleId { get; private set; }
    public string CycleCode { get; private set; } = string.Empty;
    public TreatmentMethod Method { get; private set; }
    public CyclePhase CurrentPhase { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public CycleOutcome Outcome { get; private set; }
    public string? Notes { get; private set; }

    // Extended fields
    public int? OpuNo { get; private set; }
    public int? TransferNo { get; private set; }
    public int? StimulationNo { get; private set; }
    public Guid? PlannedCycleId { get; private set; }
    public string? Room { get; private set; }
    public string? EtIuiDoctor { get; private set; }
    public bool TreatBothWifeAndEggDonor { get; private set; }
    public decimal? BetaHcg { get; private set; }
    public string? CtrlNote { get; private set; }
    public string? StopReason { get; private set; }

    // Navigation properties
    public virtual Couple Couple { get; private set; } = null!;
    public virtual ICollection<Ultrasound> Ultrasounds { get; private set; } = new List<Ultrasound>();
    public virtual ICollection<Embryo> Embryos { get; private set; } = new List<Embryo>();
    public virtual ICollection<Prescription> Prescriptions { get; private set; } = new List<Prescription>();

    // Phase-specific data navigation
    public virtual TreatmentIndication? Indication { get; private set; }
    public virtual StimulationData? Stimulation { get; private set; }
    public virtual CultureData? Culture { get; private set; }
    public virtual TransferData? Transfer { get; private set; }
    public virtual LutealPhaseData? LutealPhase { get; private set; }
    public virtual PregnancyData? Pregnancy { get; private set; }
    public virtual BirthData? Birth { get; private set; }
    public virtual ICollection<AdverseEventData> AdverseEvents { get; private set; } = new List<AdverseEventData>();

    private TreatmentCycle() { }

    public static TreatmentCycle Create(
        Guid coupleId,
        string cycleCode,
        TreatmentMethod method,
        DateTime startDate,
        string? notes = null)
    {
        return new TreatmentCycle
        {
            CoupleId = coupleId,
            CycleCode = cycleCode,
            Method = method,
            CurrentPhase = CyclePhase.Consultation,
            StartDate = startDate,
            Outcome = CycleOutcome.Ongoing,
            Notes = notes
        };
    }

    public void AdvancePhase(CyclePhase newPhase)
    {
        CurrentPhase = newPhase;
        SetUpdated();
    }

    public void Complete(CycleOutcome outcome)
    {
        CurrentPhase = CyclePhase.Completed;
        Outcome = outcome;
        EndDate = DateTime.UtcNow;
        SetUpdated();
    }

    public void Cancel()
    {
        Outcome = CycleOutcome.Cancelled;
        EndDate = DateTime.UtcNow;
        SetUpdated();
    }
}
