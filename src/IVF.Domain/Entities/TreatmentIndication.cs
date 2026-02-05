using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Treatment Indication data - Tab 1 of cycle details
/// </summary>
public class TreatmentIndication : BaseEntity
{
    public Guid CycleId { get; private set; }
    public DateTime? LastMenstruation { get; private set; }
    public string? TreatmentType { get; private set; }
    public string? Regimen { get; private set; }
    public bool FreezeAll { get; private set; }
    public bool Sis { get; private set; }
    public string? WifeDiagnosis { get; private set; }
    public string? WifeDiagnosis2 { get; private set; }
    public string? HusbandDiagnosis { get; private set; }
    public string? HusbandDiagnosis2 { get; private set; }
    public Guid? UltrasoundDoctorId { get; private set; }
    public Guid? IndicationDoctorId { get; private set; }
    public Guid? FshDoctorId { get; private set; }
    public Guid? MidwifeId { get; private set; }
    public bool Timelapse { get; private set; }
    public bool PgtA { get; private set; }
    public bool PgtSr { get; private set; }
    public bool PgtM { get; private set; }
    public string? SubType { get; private set; }
    public string? ScientificResearch { get; private set; }
    public string? Source { get; private set; }
    public string? ProcedurePlace { get; private set; }
    public string? StopReason { get; private set; }
    public DateTime? TreatmentMonth { get; private set; }
    public int PreviousTreatmentsAtSite { get; private set; }
    public int PreviousTreatmentsOther { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;

    private TreatmentIndication() { }

    public static TreatmentIndication Create(Guid cycleId)
    {
        return new TreatmentIndication { CycleId = cycleId };
    }

    public void Update(
        DateTime? lastMenstruation,
        string? treatmentType,
        string? regimen,
        bool freezeAll,
        bool sis,
        string? wifeDiagnosis,
        string? wifeDiagnosis2,
        string? husbandDiagnosis,
        string? husbandDiagnosis2,
        Guid? ultrasoundDoctorId,
        Guid? indicationDoctorId,
        Guid? fshDoctorId,
        Guid? midwifeId,
        bool timelapse,
        bool pgtA,
        bool pgtSr,
        bool pgtM,
        string? subType,
        string? scientificResearch,
        string? source,
        string? procedurePlace,
        string? stopReason,
        DateTime? treatmentMonth,
        int previousTreatmentsAtSite,
        int previousTreatmentsOther)
    {
        LastMenstruation = lastMenstruation;
        TreatmentType = treatmentType;
        Regimen = regimen;
        FreezeAll = freezeAll;
        Sis = sis;
        WifeDiagnosis = wifeDiagnosis;
        WifeDiagnosis2 = wifeDiagnosis2;
        HusbandDiagnosis = husbandDiagnosis;
        HusbandDiagnosis2 = husbandDiagnosis2;
        UltrasoundDoctorId = ultrasoundDoctorId;
        IndicationDoctorId = indicationDoctorId;
        FshDoctorId = fshDoctorId;
        MidwifeId = midwifeId;
        Timelapse = timelapse;
        PgtA = pgtA;
        PgtSr = pgtSr;
        PgtM = pgtM;
        SubType = subType;
        ScientificResearch = scientificResearch;
        Source = source;
        ProcedurePlace = procedurePlace;
        StopReason = stopReason;
        TreatmentMonth = treatmentMonth;
        PreviousTreatmentsAtSite = previousTreatmentsAtSite;
        PreviousTreatmentsOther = previousTreatmentsOther;
        SetUpdated();
    }
}
