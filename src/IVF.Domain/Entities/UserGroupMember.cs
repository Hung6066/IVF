using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Many-to-many join between User and UserGroup.
/// Tracks membership metadata for audit and compliance.
/// </summary>
public class UserGroupMember : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid GroupId { get; private set; }
    public string MemberRole { get; private set; } = "member"; // owner, admin, member
    public Guid? AddedBy { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private UserGroupMember() { }

    public static UserGroupMember Create(Guid userId, Guid groupId, string memberRole = "member", Guid? addedBy = null)
    {
        return new UserGroupMember
        {
            UserId = userId,
            GroupId = groupId,
            MemberRole = memberRole,
            AddedBy = addedBy,
            JoinedAt = DateTime.UtcNow
        };
    }

    public void UpdateRole(string memberRole)
    {
        MemberRole = memberRole;
        SetUpdated();
    }
}
