using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Contract for embryo freezing storage — stores term, fees, and renewal status
/// </summary>
public class EmbryoFreezingContract : BaseEntity, ITenantEntity
{
    public Guid CycleId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }

    public string ContractNumber { get; private set; } = string.Empty;
    public DateTime ContractDate { get; private set; }
    public DateTime StorageStartDate { get; private set; }
    public DateTime StorageEndDate { get; private set; }
    public int StorageDurationMonths { get; private set; }

    // Fees
    public decimal AnnualFee { get; private set; }
    public decimal TotalFeesPaid { get; private set; }
    public DateTime? LastPaymentDate { get; private set; }
    public DateTime? NextPaymentDue { get; private set; }

    // Status
    public string Status { get; private set; } = "Active"; // Active, Expired, Renewed, Terminated
    public string? TerminationReason { get; private set; }
    public DateTime? TerminatedAt { get; private set; }
    public Guid? TerminatedByUserId { get; private set; }

    // Signature
    public bool PatientSigned { get; private set; }
    public DateTime? PatientSignedAt { get; private set; }
    public Guid? SignedDocumentId { get; private set; }
    public string? Notes { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;
    public virtual Patient Patient { get; private set; } = null!;

    private EmbryoFreezingContract() { }

    public static EmbryoFreezingContract Create(
        Guid cycleId,
        Guid patientId,
        string contractNumber,
        DateTime contractDate,
        DateTime storageStartDate,
        int storageDurationMonths,
        decimal annualFee)
    {
        var start = storageStartDate;
        return new EmbryoFreezingContract
        {
            CycleId = cycleId,
            PatientId = patientId,
            ContractNumber = contractNumber,
            ContractDate = contractDate,
            StorageStartDate = start,
            StorageEndDate = start.AddMonths(storageDurationMonths),
            StorageDurationMonths = storageDurationMonths,
            AnnualFee = annualFee,
            TotalFeesPaid = 0,
            NextPaymentDue = start.AddYears(1),
            Status = "Active"
        };
    }

    public void RecordPayment(decimal amount)
    {
        TotalFeesPaid += amount;
        LastPaymentDate = DateTime.UtcNow;
        NextPaymentDue = NextPaymentDue?.AddYears(1);
        SetUpdated();
    }

    public void Renew(int additionalMonths)
    {
        StorageEndDate = StorageEndDate.AddMonths(additionalMonths);
        StorageDurationMonths += additionalMonths;
        Status = "Renewed";
        SetUpdated();
    }

    public void Terminate(string reason, Guid byUserId)
    {
        Status = "Terminated";
        TerminationReason = reason;
        TerminatedAt = DateTime.UtcNow;
        TerminatedByUserId = byUserId;
        SetUpdated();
    }

    public void RecordPatientSignature()
    {
        PatientSigned = true;
        PatientSignedAt = DateTime.UtcNow;
        SetUpdated();
    }
}
