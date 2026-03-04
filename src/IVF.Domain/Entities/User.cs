using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class User : BaseEntity, ITenantEntity
{
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string? Department { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiryTime { get; private set; }

    // Multi-tenancy
    public Guid TenantId { get; private set; }
    public bool IsPlatformAdmin { get; private set; } // Super admin across all tenants

    // Navigation
    public Tenant? Tenant { get; private set; }

    private User() { }

    public static User Create(
        string username,
        string passwordHash,
        string fullName,
        string role,
        string? department = null,
        Guid? tenantId = null)
    {
        return new User
        {
            Username = username,
            PasswordHash = passwordHash,
            FullName = fullName,
            Role = role,
            Department = department,
            TenantId = tenantId ?? Guid.Empty
        };
    }

    public void SetTenantId(Guid tenantId)
    {
        TenantId = tenantId;
        SetUpdated();
    }

    public void SetPlatformAdmin(bool isPlatformAdmin)
    {
        IsPlatformAdmin = isPlatformAdmin;
        SetUpdated();
    }

    public void UpdateRefreshToken(string refreshToken, DateTime expiryTime)
    {
        RefreshToken = refreshToken;
        RefreshTokenExpiryTime = expiryTime;
        SetUpdated();
    }

    public void RevokeRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiryTime = null;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }

    public void UpdateInfo(string fullName, string role, string? department)
    {
        FullName = fullName;
        Role = role;
        Department = department;
        SetUpdated();
    }

    public void UpdatePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        SetUpdated();
    }
}
