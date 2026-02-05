using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Pregnancy data - Tab 6 of cycle details
/// </summary>
public class PregnancyData : BaseEntity
{
    public Guid CycleId { get; private set; }
    public decimal? BetaHcg { get; private set; }
    public DateTime? BetaHcgDate { get; private set; }
    public bool IsPregnant { get; private set; }
    public int? GestationalSacs { get; private set; }
    public int? FetalHeartbeats { get; private set; }
    public DateTime? DueDate { get; private set; }
    public string? Notes { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;

    private PregnancyData() { }

    public static PregnancyData Create(Guid cycleId)
    {
        return new PregnancyData { CycleId = cycleId };
    }

    public void Update(
        decimal? betaHcg,
        DateTime? betaHcgDate,
        bool isPregnant,
        int? gestationalSacs,
        int? fetalHeartbeats,
        DateTime? dueDate,
        string? notes)
    {
        BetaHcg = betaHcg;
        BetaHcgDate = betaHcgDate;
        IsPregnant = isPregnant;
        GestationalSacs = gestationalSacs;
        FetalHeartbeats = fetalHeartbeats;
        DueDate = dueDate;
        Notes = notes;
        SetUpdated();
    }
}
