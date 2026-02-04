using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class Couple : BaseEntity
{
    public Guid WifeId { get; private set; }
    public Guid HusbandId { get; private set; }
    public Guid? SpermDonorId { get; private set; }
    public DateTime? MarriageDate { get; private set; }
    public int? InfertilityYears { get; private set; }

    // Navigation properties
    public virtual Patient Wife { get; private set; } = null!;
    public virtual Patient Husband { get; private set; } = null!;
    public virtual Patient? SpermDonor { get; private set; }
    public virtual ICollection<TreatmentCycle> TreatmentCycles { get; private set; } = new List<TreatmentCycle>();

    private Couple() { }

    public static Couple Create(
        Guid wifeId,
        Guid husbandId,
        DateTime? marriageDate = null,
        int? infertilityYears = null)
    {
        return new Couple
        {
            WifeId = wifeId,
            HusbandId = husbandId,
            MarriageDate = marriageDate,
            InfertilityYears = infertilityYears
        };
    }

    public void SetSpermDonor(Guid donorId)
    {
        SpermDonorId = donorId;
        SetUpdated();
    }

    public void Update(DateTime? marriageDate, int? infertilityYears)
    {
        MarriageDate = marriageDate;
        InfertilityYears = infertilityYears;
        SetUpdated();
    }
}
