using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Luteal Phase data - Tab 5 of cycle details
/// </summary>
public class LutealPhaseData : BaseEntity
{
    public Guid CycleId { get; private set; }
    public string? LutealDrug1 { get; private set; }
    public string? LutealDrug2 { get; private set; }
    public string? EndometriumDrug1 { get; private set; }
    public string? EndometriumDrug2 { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;

    private LutealPhaseData() { }

    public static LutealPhaseData Create(Guid cycleId)
    {
        return new LutealPhaseData { CycleId = cycleId };
    }

    public void Update(
        string? lutealDrug1,
        string? lutealDrug2,
        string? endometriumDrug1,
        string? endometriumDrug2)
    {
        LutealDrug1 = lutealDrug1;
        LutealDrug2 = lutealDrug2;
        EndometriumDrug1 = endometriumDrug1;
        EndometriumDrug2 = endometriumDrug2;
        SetUpdated();
    }
}
