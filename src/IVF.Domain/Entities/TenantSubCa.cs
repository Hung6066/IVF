using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Links a Tenant to its dedicated Sub-CA managed by EJBCA.
/// Each tenant references an EJBCA CA (either ManagementCA or a dedicated Sub-CA),
/// enabling:
///   - Tenant-scoped certificate issuance via EJBCA enrollment
///   - Atomic tenant offboarding (revoke all tenant certs via EJBCA)
///   - Certificate DN includes tenant organization name
///   - Per-tenant Certificate Profile and End Entity Profile
///
/// Flow: Tenant onboarding → TenantSubCa record (EJBCA CA ref) → per-user certs enrolled via EJBCA CLI/REST.
/// </summary>
public class TenantSubCa : BaseEntity
{
    /// <summary>FK to the Tenant this Sub-CA belongs to.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>
    /// EJBCA CA name used to issue certificates for this tenant.
    /// Defaults to "ManagementCA". Can be a dedicated Sub-CA created in EJBCA.
    /// </summary>
    public string EjbcaCaName { get; private set; } = null!;

    /// <summary>
    /// EJBCA Certificate Profile name for this tenant's signing certs.
    /// Defaults to "IVF-PDFSigner-Profile".
    /// </summary>
    public string EjbcaCertProfileName { get; private set; } = null!;

    /// <summary>
    /// EJBCA End Entity Profile name for this tenant's signers.
    /// Defaults to "IVF-Signer-EEProfile".
    /// </summary>
    public string EjbcaEeProfileName { get; private set; } = null!;

    /// <summary>
    /// Worker name prefix for this tenant's SignServer workers.
    /// Pattern: "PDFSigner_{tenantSlug}" — user workers append "_{username}".
    /// </summary>
    public string WorkerNamePrefix { get; private set; } = null!;

    /// <summary>Organization name used in certificate DN (O= field).</summary>
    public string OrganizationName { get; private set; } = null!;

    /// <summary>Whether automatic certificate provisioning is enabled for this tenant.</summary>
    public bool AutoProvisionEnabled { get; private set; } = true;

    /// <summary>Default certificate validity in days for user signing certs.</summary>
    public int DefaultCertValidityDays { get; private set; } = 1095; // 3 years

    /// <summary>Days before expiry to trigger auto-renewal.</summary>
    public int RenewBeforeDays { get; private set; } = 30;

    /// <summary>Maximum number of active signing workers for this tenant.</summary>
    public int MaxWorkers { get; private set; } = 50;

    /// <summary>Current count of provisioned workers.</summary>
    public int ActiveWorkerCount { get; private set; }

    public TenantSubCaStatus Status { get; private set; } = TenantSubCaStatus.Active;

    // Navigation
    public Tenant? Tenant { get; private set; }

    private TenantSubCa() { }

    public static TenantSubCa Create(
        Guid tenantId,
        string ejbcaCaName,
        string ejbcaCertProfileName,
        string ejbcaEeProfileName,
        string workerNamePrefix,
        string organizationName,
        int defaultCertValidityDays = 1095,
        int maxWorkers = 50)
    {
        return new TenantSubCa
        {
            TenantId = tenantId,
            EjbcaCaName = ejbcaCaName,
            EjbcaCertProfileName = ejbcaCertProfileName,
            EjbcaEeProfileName = ejbcaEeProfileName,
            WorkerNamePrefix = workerNamePrefix,
            OrganizationName = organizationName,
            DefaultCertValidityDays = defaultCertValidityDays,
            MaxWorkers = maxWorkers,
            Status = TenantSubCaStatus.Active
        };
    }

    public void IncrementWorkerCount()
    {
        ActiveWorkerCount++;
        SetUpdated();
    }

    public void DecrementWorkerCount()
    {
        if (ActiveWorkerCount > 0) ActiveWorkerCount--;
        SetUpdated();
    }

    public void Suspend()
    {
        Status = TenantSubCaStatus.Suspended;
        SetUpdated();
    }

    public void Activate()
    {
        Status = TenantSubCaStatus.Active;
        SetUpdated();
    }

    public void Revoke()
    {
        Status = TenantSubCaStatus.Revoked;
        SetUpdated();
    }

    public void UpdateConfig(int? validityDays, int? renewBefore, int? maxWorkerCount)
    {
        if (validityDays.HasValue) DefaultCertValidityDays = validityDays.Value;
        if (renewBefore.HasValue) RenewBeforeDays = renewBefore.Value;
        if (maxWorkerCount.HasValue) MaxWorkers = maxWorkerCount.Value;
        SetUpdated();
    }
}

public enum TenantSubCaStatus
{
    Active = 0,
    Suspended = 1,
    Revoked = 2
}
