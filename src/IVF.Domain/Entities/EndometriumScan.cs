using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Serial endometrium ultrasound scan during FET endometrial preparation monitoring
/// </summary>
public class EndometriumScan : BaseEntity, ITenantEntity
{
    public Guid CycleId { get; private set; }
    public Guid? FetProtocolId { get; private set; }
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }

    public DateTime ScanDate { get; private set; }
    public int CycleDay { get; private set; }

    // Measurements
    public decimal ThicknessMm { get; private set; }
    public string? Pattern { get; private set; } // A, B, C (triple-line / uniform / intermediate)
    public decimal? LengthMm { get; private set; }
    public decimal? WidthMm { get; private set; }
    public bool PolypsOrMyomata { get; private set; }
    public string? FluidInCavity { get; private set; } // None, Minimal, Moderate, Significant

    // Hormones (same-day results if available)
    public decimal? E2Level { get; private set; }   // pg/mL
    public decimal? LhLevel { get; private set; }   // mIU/mL
    public decimal? P4Level { get; private set; }   // ng/mL

    // Assessment
    public bool IsAdequate { get; private set; }
    public string? Recommendation { get; private set; } // Continue, AdjustDose, Cancel, Proceed
    public Guid? DoneByUserId { get; private set; }
    public string? Notes { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;
    public virtual FetProtocol? FetProtocol { get; private set; }

    private EndometriumScan() { }

    public static EndometriumScan Create(
        Guid cycleId,
        DateTime scanDate,
        int cycleDay,
        decimal thicknessMm,
        string? pattern,
        Guid? fetProtocolId = null,
        Guid? doneByUserId = null)
    {
        return new EndometriumScan
        {
            CycleId = cycleId,
            FetProtocolId = fetProtocolId,
            ScanDate = scanDate,
            CycleDay = cycleDay,
            ThicknessMm = thicknessMm,
            Pattern = pattern,
            IsAdequate = thicknessMm >= 7,
            DoneByUserId = doneByUserId
        };
    }

    public void UpdateMeasurements(
        decimal thicknessMm,
        string? pattern,
        decimal? lengthMm,
        decimal? widthMm,
        bool polypsOrMyomata,
        string? fluidInCavity)
    {
        ThicknessMm = thicknessMm;
        Pattern = pattern;
        LengthMm = lengthMm;
        WidthMm = widthMm;
        PolypsOrMyomata = polypsOrMyomata;
        FluidInCavity = fluidInCavity;
        IsAdequate = thicknessMm >= 7;
        SetUpdated();
    }

    public void RecordHormones(decimal? e2, decimal? lh, decimal? p4)
    {
        E2Level = e2;
        LhLevel = lh;
        P4Level = p4;
        SetUpdated();
    }

    public void AddRecommendation(string recommendation, string? notes = null)
    {
        Recommendation = recommendation;
        Notes = notes;
        SetUpdated();
    }
}
