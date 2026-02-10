using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class QueueTicket : BaseEntity
{
    public string TicketNumber { get; private set; } = string.Empty;
    public QueueType QueueType { get; private set; }
    public TicketPriority Priority { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? CycleId { get; private set; }
    public string DepartmentCode { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public DateTime? CalledAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? CalledByUserId { get; private set; }
    
    // Notes from service completion (e.g., consultation notes)
    public string? Notes { get; set; }

    /// <summary>
    /// Normalized join collection — replaces ServiceIndications JSON column.
    /// </summary>
    public virtual ICollection<QueueTicketService> Services { get; private set; } = new List<QueueTicketService>();

    // Navigation properties
    public virtual Patient Patient { get; private set; } = null!;
    public virtual TreatmentCycle? Cycle { get; private set; }
    public virtual User? CalledByUser { get; private set; }

    private QueueTicket() { }

    public static QueueTicket Create(
        string ticketNumber,
        QueueType queueType,
        TicketPriority priority,
        Guid patientId,
        string departmentCode,
        Guid? cycleId = null,
        IEnumerable<Guid>? serviceIds = null)
    {
        var ticket = new QueueTicket
        {
            TicketNumber = ticketNumber,
            QueueType = queueType,
            Priority = priority,
            PatientId = patientId,
            DepartmentCode = departmentCode,
            CycleId = cycleId,
            Status = TicketStatus.Waiting,
            IssuedAt = DateTime.UtcNow
        };

        if (serviceIds != null)
        {
            foreach (var serviceId in serviceIds)
                ticket.Services.Add(QueueTicketService.Create(ticket.Id, serviceId));
        }

        return ticket;
    }

    public void Call(Guid userId)
    {
        Status = TicketStatus.Called;
        CalledAt = DateTime.UtcNow;
        CalledByUserId = userId;
        SetUpdated();
    }

    public void StartService()
    {
        Status = TicketStatus.InService;
        SetUpdated();
    }

    public void Complete()
    {
        Status = TicketStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Skip()
    {
        Status = TicketStatus.Skipped;
        SetUpdated();
    }

    public void Cancel()
    {
        Status = TicketStatus.Cancelled;
        SetUpdated();
    }
}
