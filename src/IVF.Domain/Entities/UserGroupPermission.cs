using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Permissions assigned to a group — all members inherit these permissions.
/// Inspired by AWS IAM Group Policies and Google Workspace Admin Roles.
/// </summary>
public class UserGroupPermission : BaseEntity
{
    public Guid GroupId { get; private set; }
    public string PermissionCode { get; private set; } = string.Empty;
    public Guid? GrantedBy { get; private set; }
    public DateTime GrantedAt { get; private set; }

    private UserGroupPermission() { }

    public static UserGroupPermission Create(Guid groupId, string permissionCode, Guid? grantedBy = null)
    {
        return new UserGroupPermission
        {
            GroupId = groupId,
            PermissionCode = permissionCode,
            GrantedBy = grantedBy,
            GrantedAt = DateTime.UtcNow
        };
    }
}
