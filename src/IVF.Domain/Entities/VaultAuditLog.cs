using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Audit trail for all vault operations.
/// </summary>
public class VaultAuditLog : BaseEntity
{
    public string Action { get; private set; } = string.Empty;
    public string? ResourceType { get; private set; }
    public string? ResourceId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Details { get; private set; } // JSON
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    private VaultAuditLog() { }

    public static VaultAuditLog Create(
        string action,
        string? resourceType = null,
        string? resourceId = null,
        Guid? userId = null,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new VaultAuditLog
        {
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            UserId = userId,
            Details = details,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
    }
}
