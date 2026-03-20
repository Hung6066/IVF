using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public enum MatchStatus
{
    Pending,
    Matched,
    InProgress,
    Completed,
    Cancelled
}

public class EggDonorRecipient : BaseEntity, ITenantEntity
{
    private EggDonorRecipient() { }

    public static EggDonorRecipient Create(
        Guid tenantId,
        Guid eggDonorId,
        Guid recipientCoupleId,
        Guid matchedByUserId,
        string? notes) => new()
        {
            TenantId = tenantId,
            EggDonorId = eggDonorId,
            RecipientCoupleId = recipientCoupleId,
            MatchedByUserId = matchedByUserId,
            MatchedAt = DateTime.UtcNow,
            Status = MatchStatus.Matched,
            Notes = notes
        };

    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }
    public Guid EggDonorId { get; private set; }
    public Guid RecipientCoupleId { get; private set; }
    public Guid? CycleId { get; private set; }
    public Guid MatchedByUserId { get; private set; }
    public DateTime MatchedAt { get; private set; }
    public MatchStatus Status { get; private set; }
    public string? Notes { get; private set; }

    public virtual EggDonor? EggDonor { get; private set; }
    public virtual Couple? RecipientCouple { get; private set; }
    public virtual TreatmentCycle? Cycle { get; private set; }
    public virtual User? MatchedBy { get; private set; }

    public void LinkToCycle(Guid cycleId)
    {
        CycleId = cycleId;
        Status = MatchStatus.InProgress;
        SetUpdated();
    }

    public void Complete()
    {
        Status = MatchStatus.Completed;
        SetUpdated();
    }

    public void Cancel()
    {
        Status = MatchStatus.Cancelled;
        SetUpdated();
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        SetUpdated();
    }
}
