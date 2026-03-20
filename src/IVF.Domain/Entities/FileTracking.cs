using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public enum FileStatus
{
    InStorage,
    CheckedOut,
    InTransit,
    Lost,
    Archived
}

public class FileTracking : BaseEntity, ITenantEntity
{
    private FileTracking() { }

    public static FileTracking Create(
        Guid tenantId,
        Guid patientId,
        string fileCode,
        string currentLocation,
        string? notes) => new()
        {
            TenantId = tenantId,
            PatientId = patientId,
            FileCode = fileCode,
            CurrentLocation = currentLocation,
            Status = FileStatus.InStorage,
            Notes = notes,
            Transfers = new List<FileTransfer>()
        };

    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }
    public Guid PatientId { get; private set; }
    public string FileCode { get; private set; } = string.Empty;
    public string CurrentLocation { get; private set; } = string.Empty;
    public FileStatus Status { get; private set; }
    public string? Notes { get; private set; }

    public virtual Patient? Patient { get; private set; }
    public virtual ICollection<FileTransfer> Transfers { get; private set; } = new List<FileTransfer>();

    public void Transfer(string toLocation, Guid transferredByUserId, string? reason)
    {
        var transfer = FileTransfer.Create(Id, CurrentLocation, toLocation, transferredByUserId, reason);
        Transfers.Add(transfer);
        CurrentLocation = toLocation;
        Status = FileStatus.InTransit;
        SetUpdated();
    }

    public void MarkReceived()
    {
        Status = FileStatus.InStorage;
        SetUpdated();
    }

    public void MarkCheckedOut(string destination, Guid transferredByUserId)
    {
        var transfer = FileTransfer.Create(Id, CurrentLocation, destination, transferredByUserId, "Checked out");
        Transfers.Add(transfer);
        CurrentLocation = destination;
        Status = FileStatus.CheckedOut;
        SetUpdated();
    }

    public void MarkLost(string? reason)
    {
        Status = FileStatus.Lost;
        Notes = reason;
        SetUpdated();
    }
}

public class FileTransfer : BaseEntity
{
    private FileTransfer() { }

    public static FileTransfer Create(Guid fileTrackingId, string fromLocation, string toLocation,
        Guid transferredByUserId, string? reason) => new()
        {
            FileTrackingId = fileTrackingId,
            FromLocation = fromLocation,
            ToLocation = toLocation,
            TransferredByUserId = transferredByUserId,
            Reason = reason,
            TransferredAt = DateTime.UtcNow
        };

    public Guid FileTrackingId { get; private set; }
    public string FromLocation { get; private set; } = string.Empty;
    public string ToLocation { get; private set; } = string.Empty;
    public Guid TransferredByUserId { get; private set; }
    public string? Reason { get; private set; }
    public DateTime TransferredAt { get; private set; }

    public virtual FileTracking? File { get; private set; }
    public virtual User? TransferredBy { get; private set; }
}
