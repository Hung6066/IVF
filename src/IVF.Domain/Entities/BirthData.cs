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
    public string? BabyGenders { get; private set; }
    public string? BirthWeights { get; private set; }
    public string? Complications { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;

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
        string? babyGenders,
        string? birthWeights,
        string? complications)
    {
        DeliveryDate = deliveryDate;
        GestationalWeeks = gestationalWeeks;
        DeliveryMethod = deliveryMethod;
        LiveBirths = liveBirths;
        Stillbirths = stillbirths;
        BabyGenders = babyGenders;
        BirthWeights = birthWeights;
        Complications = complications;
        SetUpdated();
    }
}
