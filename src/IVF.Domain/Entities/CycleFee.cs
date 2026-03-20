using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class CycleFee : BaseEntity, ITenantEntity
{
    public Guid CycleId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }

    public string FeeType { get; private set; } = string.Empty; // FollicleScan, OPU, IUI, FET, EmbryoTransfer, EmbryoFreeze, SpermWashing, Anesthesia
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal BalanceDue => Amount - PaidAmount;

    public string Status { get; private set; } = "Pending"; // Pending, Invoiced, Paid, Waived, Refunded
    public Guid? InvoiceId { get; private set; }
    public bool IsOneTimePerCycle { get; private set; } // Chỉ thu 1 lần/chu kỳ (e.g. SA nang noãn)
    public DateTime? WaivedAt { get; private set; }
    public string? WaivedReason { get; private set; }
    public Guid? WaivedByUserId { get; private set; }
    public string? Notes { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;
    public virtual Patient Patient { get; private set; } = null!;
    public virtual Invoice? Invoice { get; private set; }

    private CycleFee() { }

    public static CycleFee Create(
        Guid cycleId,
        Guid patientId,
        string feeType,
        string description,
        decimal amount,
        bool isOneTimePerCycle = false,
        string? notes = null)
    {
        return new CycleFee
        {
            CycleId = cycleId,
            PatientId = patientId,
            FeeType = feeType,
            Description = description,
            Amount = amount,
            IsOneTimePerCycle = isOneTimePerCycle,
            Notes = notes
        };
    }

    public void LinkToInvoice(Guid invoiceId)
    {
        InvoiceId = invoiceId;
        Status = "Invoiced";
        SetUpdated();
    }

    public void RecordPayment(decimal amount)
    {
        PaidAmount += amount;
        if (PaidAmount >= Amount)
            Status = "Paid";
        SetUpdated();
    }

    public void Waive(Guid waivedByUserId, string reason)
    {
        Status = "Waived";
        WaivedAt = DateTime.UtcNow;
        WaivedByUserId = waivedByUserId;
        WaivedReason = reason;
        SetUpdated();
    }

    public void Refund()
    {
        Status = "Refunded";
        SetUpdated();
    }
}
