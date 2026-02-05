using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Transfer data - Tab 4 of cycle details
/// </summary>
public class TransferData : BaseEntity
{
    public Guid CycleId { get; private set; }
    public DateTime? TransferDate { get; private set; }
    public DateTime? ThawingDate { get; private set; }
    public int DayOfTransfered { get; private set; }
    public string? LabNote { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;

    private TransferData() { }

    public static TransferData Create(Guid cycleId)
    {
        return new TransferData { CycleId = cycleId };
    }

    public void Update(
        DateTime? transferDate,
        DateTime? thawingDate,
        int dayOfTransfered,
        string? labNote)
    {
        TransferDate = transferDate;
        ThawingDate = thawingDate;
        DayOfTransfered = dayOfTransfered;
        LabNote = labNote;
        SetUpdated();
    }
}
