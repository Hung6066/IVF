using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class Embryo : BaseEntity
{
    public Guid CycleId { get; private set; }
    public int EmbryoNumber { get; private set; }
    public DateTime FertilizationDate { get; private set; }
    public EmbryoGrade? Grade { get; private set; }
    public EmbryoDay Day { get; private set; }
    public EmbryoStatus Status { get; private set; }
    public Guid? CryoLocationId { get; private set; }
    public DateTime? FreezeDate { get; private set; }
    public DateTime? ThawDate { get; private set; }
    public string? Notes { get; private set; }

    // Navigation properties
    public virtual TreatmentCycle Cycle { get; private set; } = null!;
    public virtual CryoLocation? CryoLocation { get; private set; }

    private Embryo() { }

    public static Embryo Create(
        Guid cycleId,
        int embryoNumber,
        DateTime fertilizationDate)
    {
        return new Embryo
        {
            CycleId = cycleId,
            EmbryoNumber = embryoNumber,
            FertilizationDate = fertilizationDate,
            Day = EmbryoDay.D1,
            Status = EmbryoStatus.Developing
        };
    }

    public void UpdateGrade(EmbryoGrade grade, EmbryoDay day)
    {
        Grade = grade;
        Day = day;
        SetUpdated();
    }

    public void Transfer()
    {
        Status = EmbryoStatus.Transferred;
        SetUpdated();
    }

    public void Freeze(Guid cryoLocationId)
    {
        Status = EmbryoStatus.Frozen;
        CryoLocationId = cryoLocationId;
        FreezeDate = DateTime.UtcNow;
        SetUpdated();
    }

    public void Thaw()
    {
        Status = EmbryoStatus.Thawed;
        ThawDate = DateTime.UtcNow;
        SetUpdated();
    }

    public void Discard(string reason)
    {
        Status = EmbryoStatus.Discarded;
        Notes = reason;
        SetUpdated();
    }

    public void MarkArrested()
    {
        Status = EmbryoStatus.Arrested;
        SetUpdated();
    }
}
