using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Permission delegation from one user to another with time-bounded scope.
/// Enables Doctor → Nurse delegation patterns without permanent permission assignment.
/// </summary>
public class PermissionDelegation : BaseEntity
{
    public Guid FromUserId { get; private set; } // Delegator
    public Guid ToUserId { get; private set; } // Delegatee
    public string Permissions { get; private set; } = "[]"; // JSON: ["ViewPatients","PerformUltrasound"]
    public string? Reason { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public DateTime ValidUntil { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }

    private PermissionDelegation() { }

    public static PermissionDelegation Create(
        Guid fromUserId,
        Guid toUserId,
        string permissionsJson,
        string? reason,
        DateTime validFrom,
        DateTime validUntil)
    {
        return new PermissionDelegation
        {
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Permissions = permissionsJson,
            Reason = reason,
            ValidFrom = validFrom,
            ValidUntil = validUntil
        };
    }

    public bool IsActive() => !IsRevoked && !IsDeleted &&
        DateTime.UtcNow >= ValidFrom && DateTime.UtcNow <= ValidUntil;

    public void Revoke(string? reason)
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        RevokedReason = reason;
        SetUpdated();
    }
}
