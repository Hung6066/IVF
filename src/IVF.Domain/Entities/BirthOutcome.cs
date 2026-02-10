using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Individual baby outcome within a birth event — normalizes BabyGenders/BirthWeights CSV columns.
/// </summary>
public class BirthOutcome : BaseEntity
{
    public Guid BirthDataId { get; private set; }
    public int SortOrder { get; private set; }
    public string Gender { get; private set; } = string.Empty;
    public decimal? Weight { get; private set; }
    public bool IsLiveBirth { get; private set; } = true;

    // Navigation
    public virtual BirthData BirthData { get; private set; } = null!;

    private BirthOutcome() { }

    public static BirthOutcome Create(Guid birthDataId, int sortOrder, string gender, decimal? weight, bool isLiveBirth = true)
    {
        return new BirthOutcome
        {
            BirthDataId = birthDataId,
            SortOrder = sortOrder,
            Gender = gender,
            Weight = weight,
            IsLiveBirth = isLiveBirth
        };
    }

    public void Update(string gender, decimal? weight, bool isLiveBirth)
    {
        Gender = gender;
        Weight = weight;
        IsLiveBirth = isLiveBirth;
        SetUpdated();
    }
}
