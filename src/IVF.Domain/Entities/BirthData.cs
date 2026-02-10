using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Birth data - Tab 7 of cycle details
/// </summary>
public class BirthData : BaseEntity
{
    public Guid CycleId { get; private set; }
    public DateTime? DeliveryDate { get; private set; }
    public int GestationalWeeks { get; private set; }
    public string? DeliveryMethod { get; private set; }
    public int LiveBirths { get; private set; }
    public int Stillbirths { get; private set; }
    public string? Complications { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;

    /// <summary>
    /// Normalized child collection — replaces BabyGenders/BirthWeights CSV columns.
    /// </summary>
    public virtual ICollection<BirthOutcome> Outcomes { get; private set; } = new List<BirthOutcome>();

    private BirthData() { }

    public static BirthData Create(Guid cycleId)
    {
        return new BirthData { CycleId = cycleId };
    }

    public void Update(
        DateTime? deliveryDate,
        int gestationalWeeks,
        string? deliveryMethod,
        int liveBirths,
        int stillbirths,
        string? complications)
    {
        DeliveryDate = deliveryDate;
        GestationalWeeks = gestationalWeeks;
        DeliveryMethod = deliveryMethod;
        LiveBirths = liveBirths;
        Stillbirths = stillbirths;
        Complications = complications;
        SetUpdated();
    }

    public void SetOutcomes(IEnumerable<BirthOutcome> outcomes)
    {
        Outcomes.Clear();
        foreach (var outcome in outcomes)
            Outcomes.Add(outcome);
        SetUpdated();
    }
}
