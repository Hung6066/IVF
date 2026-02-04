using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// AuditLog entity for tracking all entity changes
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid? UserId { get; private set; }
    public string? Username { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;  // Create, Update, Delete
    public string? OldValues { get; private set; }  // JSON
    public string? NewValues { get; private set; }  // JSON
    public string? ChangedColumns { get; private set; }  // Comma-separated
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    
    private AuditLog() { }
    
    public static AuditLog Create(
        string entityType,
        Guid entityId,
        string action,
        Guid? userId = null,
        string? username = null,
        string? oldValues = null,
        string? newValues = null,
        string? changedColumns = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = userId,
            Username = username,
            OldValues = oldValues,
            NewValues = newValues,
            ChangedColumns = changedColumns,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
    }
}
