using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? LogoUrl { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? TaxId { get; private set; }
    public string? Website { get; private set; }
    public TenantStatus Status { get; private set; } = TenantStatus.PendingSetup;

    // Resource limits
    public int MaxUsers { get; private set; } = 5;
    public int MaxPatientsPerMonth { get; private set; } = 50;
    public long StorageLimitMb { get; private set; } = 1024; // 1 GB
    public bool AiEnabled { get; private set; }
    public bool DigitalSigningEnabled { get; private set; }
    public bool BiometricsEnabled { get; private set; }
    public bool AdvancedReportingEnabled { get; private set; }

    // Database isolation
    public DataIsolationStrategy IsolationStrategy { get; private set; } = DataIsolationStrategy.SharedDatabase;
    public string? ConnectionString { get; private set; } // null = shared DB with row-level isolation
    public string? DatabaseSchema { get; private set; }
    public bool IsRootTenant { get; private set; } // Root tenant = platform admin tenant

    // Customization
    public string? PrimaryColor { get; private set; }
    public string? Locale { get; private set; } = "vi-VN";
    public string? TimeZone { get; private set; } = "Asia/Ho_Chi_Minh";
    public string? CustomDomain { get; private set; }
    public CustomDomainStatus CustomDomainStatus { get; private set; } = CustomDomainStatus.None;
    public DateTime? CustomDomainVerifiedAt { get; private set; }
    public string? CustomDomainVerificationToken { get; private set; }

    private Tenant() { }

    public static Tenant Create(
        string name,
        string slug,
        string? email = null,
        string? phone = null,
        string? address = null)
    {
        return new Tenant
        {
            Name = name,
            Slug = slug.ToLowerInvariant(),
            Email = email,
            Phone = phone,
            Address = address,
            Status = TenantStatus.PendingSetup
        };
    }

    public void Activate()
    {
        Status = TenantStatus.Active;
        SetUpdated();
    }

    public void Suspend(string? _reason = null)
    {
        Status = TenantStatus.Suspended;
        SetUpdated();
    }

    public void Cancel()
    {
        Status = TenantStatus.Cancelled;
        SetUpdated();
    }

    public void StartTrial()
    {
        Status = TenantStatus.Trial;
        SetUpdated();
    }

    public void UpdateInfo(string name, string? address, string? phone, string? email, string? website, string? taxId)
    {
        Name = name;
        Address = address;
        Phone = phone;
        Email = email;
        Website = website;
        TaxId = taxId;
        SetUpdated();
    }

    public void UpdateBranding(string? logoUrl, string? primaryColor, string? customDomain)
    {
        LogoUrl = logoUrl;
        PrimaryColor = primaryColor;

        // If domain changed, reset verification
        if (!string.Equals(CustomDomain, customDomain, StringComparison.OrdinalIgnoreCase))
        {
            CustomDomain = string.IsNullOrWhiteSpace(customDomain) ? null : customDomain.Trim().ToLowerInvariant();
            CustomDomainStatus = string.IsNullOrWhiteSpace(customDomain) ? CustomDomainStatus.None : CustomDomainStatus.Pending;
            CustomDomainVerifiedAt = null;
            CustomDomainVerificationToken = string.IsNullOrWhiteSpace(customDomain) ? null : GenerateVerificationToken();
        }
        SetUpdated();
    }

    public void VerifyCustomDomain()
    {
        CustomDomainStatus = CustomDomainStatus.Verified;
        CustomDomainVerifiedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void FailCustomDomainVerification()
    {
        CustomDomainStatus = CustomDomainStatus.Failed;
        SetUpdated();
    }

    public void RemoveCustomDomain()
    {
        CustomDomain = null;
        CustomDomainStatus = CustomDomainStatus.None;
        CustomDomainVerifiedAt = null;
        CustomDomainVerificationToken = null;
        SetUpdated();
    }

    private static string GenerateVerificationToken()
    {
        return $"ivf-verify-{Guid.NewGuid():N}";
    }

    public void SetResourceLimits(int maxUsers, int maxPatientsPerMonth, long storageLimitMb,
        bool aiEnabled, bool digitalSigningEnabled, bool biometricsEnabled, bool advancedReportingEnabled)
    {
        MaxUsers = maxUsers;
        MaxPatientsPerMonth = maxPatientsPerMonth;
        StorageLimitMb = storageLimitMb;
        AiEnabled = aiEnabled;
        DigitalSigningEnabled = digitalSigningEnabled;
        BiometricsEnabled = biometricsEnabled;
        AdvancedReportingEnabled = advancedReportingEnabled;
        SetUpdated();
    }

    public void SetDatabaseIsolation(DataIsolationStrategy strategy, string? connectionString, string? schema)
    {
        IsolationStrategy = strategy;
        ConnectionString = strategy == DataIsolationStrategy.SeparateDatabase ? connectionString : null;
        DatabaseSchema = strategy == DataIsolationStrategy.SeparateSchema ? schema : null;
        SetUpdated();
    }

    public void SetRootTenant(bool isRoot)
    {
        IsRootTenant = isRoot;
        SetUpdated();
    }
}
