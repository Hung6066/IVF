using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class CryoLocation : BaseEntity
{
    public string Tank { get; private set; } = string.Empty;
    public string Canister { get; private set; } = string.Empty;
    public string Cane { get; private set; } = string.Empty;
    public string Goblet { get; private set; } = string.Empty;
    public string Straw { get; private set; } = string.Empty;
    public SpecimenType SpecimenType { get; private set; }
    public bool IsOccupied { get; private set; }

    private CryoLocation() { }

    public static CryoLocation Create(
        string tank,
        string canister,
        string cane,
        string goblet,
        string straw,
        SpecimenType specimenType)
    {
        return new CryoLocation
        {
            Tank = tank,
            Canister = canister,
            Cane = cane,
            Goblet = goblet,
            Straw = straw,
            SpecimenType = specimenType,
            IsOccupied = false
        };
    }

    public string GetFullLocation() => $"{Tank}-{Canister}-{Cane}-{Goblet}-{Straw}";

    public void Occupy()
    {
        IsOccupied = true;
        SetUpdated();
    }

    public void Release()
    {
        IsOccupied = false;
        SetUpdated();
    }
}
