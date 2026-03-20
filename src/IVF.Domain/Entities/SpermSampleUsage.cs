using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Tracks usage of a SpermSample in a treatment cycle (IUI, ICSI, IVF)
/// </summary>
public class SpermSampleUsage : BaseEntity, ITenantEntity
{
    public Guid SpermSampleId { get; private set; }
    public Guid CycleId { get; private set; }
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }

    public DateTime UsageDate { get; private set; }
    public string Procedure { get; private set; } = string.Empty; // IUI, ICSI, IVF, IUI-D
    public int VialsUsed { get; private set; }
    public Guid? AuthorizedByUserId { get; private set; }
    public Guid? PerformedByUserId { get; private set; }

    // Post-thaw quality (for frozen samples)
    public decimal? PostThawMotility { get; private set; }
    public int? PostThawConcentration { get; private set; }
    public string? PostThawNotes { get; private set; }

    public string? Notes { get; private set; }

    // Navigation
    public virtual SpermSample SpermSample { get; private set; } = null!;
    public virtual TreatmentCycle Cycle { get; private set; } = null!;

    private SpermSampleUsage() { }

    public static SpermSampleUsage Create(
        Guid spermSampleId,
        Guid cycleId,
        DateTime usageDate,
        string procedure,
        int vialsUsed,
        Guid? authorizedByUserId = null,
        Guid? performedByUserId = null)
    {
        return new SpermSampleUsage
        {
            SpermSampleId = spermSampleId,
            CycleId = cycleId,
            UsageDate = usageDate,
            Procedure = procedure,
            VialsUsed = vialsUsed,
            AuthorizedByUserId = authorizedByUserId,
            PerformedByUserId = performedByUserId
        };
    }

    public void RecordPostThawQuality(decimal? motility, int? concentration, string? notes)
    {
        PostThawMotility = motility;
        PostThawConcentration = concentration;
        PostThawNotes = notes;
        SetUpdated();
    }
}
