using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Notification entity for user notifications
/// </summary>
public class Notification : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public NotificationType Type { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public string? EntityType { get; private set; }  // e.g., "Appointment", "QueueTicket"
    public Guid? EntityId { get; private set; }
    
    // Navigation
    public virtual User User { get; private set; } = null!;
    
    private Notification() { }
    
    public static Notification Create(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? entityType = null,
        Guid? entityId = null)
    {
        return new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            EntityType = entityType,
            EntityId = entityId
        };
    }
    
    public void MarkAsRead()
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadAt = DateTime.UtcNow;
            SetUpdated();
        }
    }
}

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error,
    AppointmentReminder,
    QueueCalled,
    CycleUpdate,
    PaymentDue
}
