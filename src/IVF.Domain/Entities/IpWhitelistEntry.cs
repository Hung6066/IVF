using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class IpWhitelistEntry : BaseEntity
{
    public string IpAddress { get; private set; } = string.Empty;
    public string? CidrRange { get; private set; }
    public string? Description { get; private set; }
    public string AddedBy { get; private set; } = string.Empty;
    public DateTime? ExpiresAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    private IpWhitelistEntry() { }

    public static IpWhitelistEntry Create(
        string ipAddress,
        string? description,
        string addedBy,
        int? expiresInDays = null,
        string? cidrRange = null)
    {
        return new IpWhitelistEntry
        {
            IpAddress = ipAddress,
            CidrRange = cidrRange,
            Description = description,
            AddedBy = addedBy,
            ExpiresAt = expiresInDays.HasValue
                ? DateTime.UtcNow.AddDays(expiresInDays.Value)
                : null,
            IsActive = true
        };
    }

    public bool IsCurrentlyActive() => IsActive && !IsDeleted && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Update(string? description, int? expiresInDays)
    {
        Description = description;
        if (expiresInDays.HasValue)
            ExpiresAt = DateTime.UtcNow.AddDays(expiresInDays.Value);
        SetUpdated();
    }
}
