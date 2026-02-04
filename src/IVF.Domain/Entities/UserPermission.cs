using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Join entity for User-Permission many-to-many relationship
/// </summary>
public class UserPermission : BaseEntity
{
    public Guid UserId { get; private set; }
    public Permission Permission { get; private set; }
    public Guid? GrantedBy { get; private set; }
    public DateTime GrantedAt { get; private set; }
    
    // Navigation
    public virtual User User { get; private set; } = null!;
    
    private UserPermission() { }
    
    public static UserPermission Create(Guid userId, Permission permission, Guid? grantedBy = null)
    {
        return new UserPermission
        {
            UserId = userId,
            Permission = permission,
            GrantedBy = grantedBy,
            GrantedAt = DateTime.UtcNow
        };
    }
}
