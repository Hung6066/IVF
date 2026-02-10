using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Individual stimulation drug — normalizes Drug1-4/Duration/Posology repeated column groups.
/// </summary>
public class StimulationDrug : BaseEntity
{
    public Guid StimulationDataId { get; private set; }
    public int SortOrder { get; private set; }
    public string DrugName { get; private set; } = string.Empty;
    public int Duration { get; private set; }
    public string? Posology { get; private set; }

    // Navigation
    public virtual StimulationData StimulationData { get; private set; } = null!;

    private StimulationDrug() { }

    public static StimulationDrug Create(Guid stimulationDataId, int sortOrder, string drugName, int duration, string? posology)
    {
        return new StimulationDrug
        {
            StimulationDataId = stimulationDataId,
            SortOrder = sortOrder,
            DrugName = drugName,
            Duration = duration,
            Posology = posology
        };
    }

    public void Update(string drugName, int duration, string? posology)
    {
        DrugName = drugName;
        Duration = duration;
        Posology = posology;
        SetUpdated();
    }
}
