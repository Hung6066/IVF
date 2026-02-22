using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Join entity for User-Permission many-to-many relationship.
/// Permission is stored as a string code (e.g. "ViewPatients") to support
/// dynamically-created permissions from the PermissionDefinition table.
/// </summary>
public class UserPermission : BaseEntity
{
    public Guid UserId { get; private set; }
    public string PermissionCode { get; private set; } = string.Empty;
    public Guid? GrantedBy { get; private set; }
    public DateTime GrantedAt { get; private set; }

    // Navigation
    public virtual User User { get; private set; } = null!;

    private UserPermission() { }

    public static UserPermission Create(Guid userId, string permissionCode, Guid? grantedBy = null)
    {
        return new UserPermission
        {
            UserId = userId,
            PermissionCode = permissionCode,
            GrantedBy = grantedBy,
            GrantedAt = DateTime.UtcNow
        };
    }
}
