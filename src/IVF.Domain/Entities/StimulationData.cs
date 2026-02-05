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

    // Drug 1-4 with Duration and Posology
    public string? Drug1 { get; private set; }
    public int Drug1Duration { get; private set; }
    public string? Drug1Posology { get; private set; }
    public string? Drug2 { get; private set; }
    public int Drug2Duration { get; private set; }
    public string? Drug2Posology { get; private set; }
    public string? Drug3 { get; private set; }
    public int Drug3Duration { get; private set; }
    public string? Drug3Posology { get; private set; }
    public string? Drug4 { get; private set; }
    public int Drug4Duration { get; private set; }
    public string? Drug4Posology { get; private set; }

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
        string? drug1, int drug1Duration, string? drug1Posology,
        string? drug2, int drug2Duration, string? drug2Posology,
        string? drug3, int drug3Duration, string? drug3Posology,
        string? drug4, int drug4Duration, string? drug4Posology,
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
        Drug1 = drug1; Drug1Duration = drug1Duration; Drug1Posology = drug1Posology;
        Drug2 = drug2; Drug2Duration = drug2Duration; Drug2Posology = drug2Posology;
        Drug3 = drug3; Drug3Duration = drug3Duration; Drug3Posology = drug3Posology;
        Drug4 = drug4; Drug4Duration = drug4Duration; Drug4Posology = drug4Posology;
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
}
