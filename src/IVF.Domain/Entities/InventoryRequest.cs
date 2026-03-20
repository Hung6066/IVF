using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public enum InventoryRequestType
{
    Restock,
    Usage,
    PurchaseOrder,
    Return
}

public enum InventoryRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Fulfilled,
    Cancelled
}

public class InventoryRequest : BaseEntity, ITenantEntity
{
    private InventoryRequest() { }

    public static InventoryRequest Create(
        Guid tenantId,
        InventoryRequestType requestType,
        Guid requestedByUserId,
        string itemName,
        int quantity,
        string unit,
        string? reason,
        string? notes) => new()
        {
            TenantId = tenantId,
            RequestType = requestType,
            RequestedByUserId = requestedByUserId,
            ItemName = itemName,
            Quantity = quantity,
            Unit = unit,
            Reason = reason,
            Notes = notes,
            Status = InventoryRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }
    public InventoryRequestType RequestType { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public string ItemName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? Notes { get; private set; }
    public InventoryRequestStatus Status { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    public virtual User? RequestedBy { get; private set; }
    public virtual User? ApprovedBy { get; private set; }

    public void Approve(Guid approvedByUserId)
    {
        Status = InventoryRequestStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ProcessedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Reject(Guid rejectedByUserId, string reason)
    {
        Status = InventoryRequestStatus.Rejected;
        ApprovedByUserId = rejectedByUserId;
        RejectionReason = reason;
        ProcessedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Fulfill()
    {
        Status = InventoryRequestStatus.Fulfilled;
        ProcessedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Cancel()
    {
        Status = InventoryRequestStatus.Cancelled;
        SetUpdated();
    }
}
