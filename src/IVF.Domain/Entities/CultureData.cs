using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Culture data - Tab 3 of cycle details
/// </summary>
public class CultureData : BaseEntity
{
    public Guid CycleId { get; private set; }
    public int TotalFreezedEmbryo { get; private set; }
    public int TotalThawedEmbryo { get; private set; }
    public int TotalTransferedEmbryo { get; private set; }
    public int RemainFreezedEmbryo { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;

    private CultureData() { }

    public static CultureData Create(Guid cycleId)
    {
        return new CultureData { CycleId = cycleId };
    }

    public void Update(
        int totalFreezedEmbryo,
        int totalThawedEmbryo,
        int totalTransferedEmbryo,
        int remainFreezedEmbryo)
    {
        TotalFreezedEmbryo = totalFreezedEmbryo;
        TotalThawedEmbryo = totalThawedEmbryo;
        TotalTransferedEmbryo = totalTransferedEmbryo;
        RemainFreezedEmbryo = remainFreezedEmbryo;
        SetUpdated();
    }
}
