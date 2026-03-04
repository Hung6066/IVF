using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class TenantUsageRecord : BaseEntity
{
    public Guid TenantId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public int ActiveUsers { get; private set; }
    public int NewPatients { get; private set; }
    public int TreatmentCycles { get; private set; }
    public int FormResponses { get; private set; }
    public int SignedDocuments { get; private set; }
    public long StorageUsedMb { get; private set; }
    public int ApiCalls { get; private set; }

    // Navigation
    public Tenant Tenant { get; private set; } = null!;

    private TenantUsageRecord() { }

    public static TenantUsageRecord Create(Guid tenantId, int year, int month)
    {
        return new TenantUsageRecord
        {
            TenantId = tenantId,
            Year = year,
            Month = month
        };
    }

    public void UpdateUsage(int activeUsers, int newPatients, int treatmentCycles,
        int formResponses, int signedDocuments, long storageUsedMb, int apiCalls)
    {
        ActiveUsers = activeUsers;
        NewPatients = newPatients;
        TreatmentCycles = treatmentCycles;
        FormResponses = formResponses;
        SignedDocuments = signedDocuments;
        StorageUsedMb = storageUsedMb;
        ApiCalls = apiCalls;
        SetUpdated();
    }

    public void IncrementApiCalls(int count = 1)
    {
        ApiCalls += count;
        SetUpdated();
    }
}
