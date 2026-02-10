using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Stimulation & Trigger data - Tab 2 of cycle details
/// </summary>
public class StimulationData : BaseEntity
{
    public Guid CycleId { get; private set; }

    // Stimulation Protocol
    public DateTime? LastMenstruation { get; private set; }
    public DateTime? StartDate { get; private set; }
    public int? StartDay { get; private set; }

    /// <summary>
    /// Normalized child collection — replaces Drug1-4/Duration/Posology repeated column groups.
    /// </summary>
    public virtual ICollection<StimulationDrug> Drugs { get; private set; } = new List<StimulationDrug>();

    // Follicle monitoring
    public int? Size12Follicle { get; private set; }
    public int? Size14Follicle { get; private set; }
    public decimal? EndometriumThickness { get; private set; }

    // Trigger
    public string? TriggerDrug { get; private set; }
    public string? TriggerDrug2 { get; private set; }
    public DateTime? HcgDate { get; private set; }
    public DateTime? HcgDate2 { get; private set; }
    public TimeSpan? HcgTime { get; private set; }
    public TimeSpan? HcgTime2 { get; private set; }
    public decimal? LhLab { get; private set; }
    public decimal? E2Lab { get; private set; }
    public decimal? P4Lab { get; private set; }

    // Aspiration
    public string? ProcedureType { get; private set; }
    public DateTime? AspirationDate { get; private set; }
    public DateTime? ProcedureDate { get; private set; }
    public int? AspirationNo { get; private set; }
    public string? TechniqueWife { get; private set; }
    public string? TechniqueHusband { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;

    private StimulationData() { }

    public static StimulationData Create(Guid cycleId)
    {
        return new StimulationData { CycleId = cycleId };
    }

    public void Update(
        DateTime? lastMenstruation,
        DateTime? startDate,
        int? startDay,
        int? size12Follicle,
        int? size14Follicle,
        decimal? endometriumThickness,
        string? triggerDrug,
        string? triggerDrug2,
        DateTime? hcgDate,
        DateTime? hcgDate2,
        TimeSpan? hcgTime,
        TimeSpan? hcgTime2,
        decimal? lhLab,
        decimal? e2Lab,
        decimal? p4Lab,
        string? procedureType,
        DateTime? aspirationDate,
        DateTime? procedureDate,
        int? aspirationNo,
        string? techniqueWife,
        string? techniqueHusband)
    {
        LastMenstruation = lastMenstruation;
        StartDate = startDate;
        StartDay = startDay;
        Size12Follicle = size12Follicle;
        Size14Follicle = size14Follicle;
        EndometriumThickness = endometriumThickness;
        TriggerDrug = triggerDrug;
        TriggerDrug2 = triggerDrug2;
        HcgDate = hcgDate;
        HcgDate2 = hcgDate2;
        HcgTime = hcgTime;
        HcgTime2 = hcgTime2;
        LhLab = lhLab;
        E2Lab = e2Lab;
        P4Lab = p4Lab;
        ProcedureType = procedureType;
        AspirationDate = aspirationDate;
        ProcedureDate = procedureDate;
        AspirationNo = aspirationNo;
        TechniqueWife = techniqueWife;
        TechniqueHusband = techniqueHusband;
        SetUpdated();
    }

    public void SetDrugs(IEnumerable<StimulationDrug> drugs)
    {
        Drugs.Clear();
        foreach (var drug in drugs)
            Drugs.Add(drug);
        SetUpdated();
    }
}
