using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Individual luteal phase drug — normalizes LutealDrug1/2 and EndometriumDrug1/2 repeated columns.
/// Category distinguishes between Luteal support and Endometrium preparation drugs.
/// </summary>
public class LutealPhaseDrug : BaseEntity
{
    public Guid LutealPhaseDataId { get; private set; }
    public int SortOrder { get; private set; }
    public string DrugName { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty; // "Luteal" or "Endometrium"

    // Navigation
    public virtual LutealPhaseData LutealPhaseData { get; private set; } = null!;

    private LutealPhaseDrug() { }

    public static LutealPhaseDrug Create(Guid lutealPhaseDataId, int sortOrder, string drugName, string category)
    {
        return new LutealPhaseDrug
        {
            LutealPhaseDataId = lutealPhaseDataId,
            SortOrder = sortOrder,
            DrugName = drugName,
            Category = category
        };
    }

    public void Update(string drugName, string category)
    {
        DrugName = drugName;
        Category = category;
        SetUpdated();
    }
}
