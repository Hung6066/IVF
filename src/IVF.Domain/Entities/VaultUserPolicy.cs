using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class VaultUserPolicy : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid PolicyId { get; private set; }
    public DateTime GrantedAt { get; private set; } = DateTime.UtcNow;
    public Guid? GrantedBy { get; private set; }

    // Navigation
    public VaultPolicy? Policy { get; private set; }
    public User? User { get; private set; }

    private VaultUserPolicy() { }

    public static VaultUserPolicy Create(Guid userId, Guid policyId, Guid? grantedBy = null)
    {
        return new VaultUserPolicy
        {
            UserId = userId,
            PolicyId = policyId,
            GrantedBy = grantedBy
        };
    }
}
