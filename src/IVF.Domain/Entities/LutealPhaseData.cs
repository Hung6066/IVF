using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Luteal Phase data - Tab 5 of cycle details
/// </summary>
public class LutealPhaseData : BaseEntity
{
    public Guid CycleId { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;

    /// <summary>
    /// Normalized child collection — replaces LutealDrug1/2 and EndometriumDrug1/2 repeated columns.
    /// </summary>
    public virtual ICollection<LutealPhaseDrug> Drugs { get; private set; } = new List<LutealPhaseDrug>();

    private LutealPhaseData() { }

    public static LutealPhaseData Create(Guid cycleId)
    {
        return new LutealPhaseData { CycleId = cycleId };
    }

    public void Update() => SetUpdated();

    public void SetDrugs(IEnumerable<LutealPhaseDrug> drugs)
    {
        Drugs.Clear();
        foreach (var drug in drugs)
            Drugs.Add(drug);
        SetUpdated();
    }
}
