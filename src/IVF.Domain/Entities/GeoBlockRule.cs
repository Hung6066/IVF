using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class GeoBlockRule : BaseEntity
{
    public string CountryCode { get; private set; } = string.Empty;
    public string CountryName { get; private set; } = string.Empty;
    public bool IsBlocked { get; private set; }
    public string? Reason { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; } = true;

    private GeoBlockRule() { }

    public static GeoBlockRule Create(
        string countryCode,
        string countryName,
        bool isBlocked,
        string createdBy,
        string? reason = null)
    {
        return new GeoBlockRule
        {
            CountryCode = countryCode,
            CountryName = countryName,
            IsBlocked = isBlocked,
            Reason = reason,
            CreatedBy = createdBy,
            IsEnabled = true
        };
    }

    public void Update(bool isBlocked, string? reason)
    {
        IsBlocked = isBlocked;
        Reason = reason;
        SetUpdated();
    }

    public void Enable() { IsEnabled = true; SetUpdated(); }
    public void Disable() { IsEnabled = false; SetUpdated(); }
}
