using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class VaultLease : BaseEntity
{
    public string LeaseId { get; private set; } = string.Empty;
    public Guid SecretId { get; private set; }
    public int Ttl { get; private set; }
    public bool Renewable { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool Revoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    // Navigation
    public VaultSecret? Secret { get; private set; }

    private VaultLease() { }

    public static VaultLease Create(Guid secretId, int ttlSeconds, bool renewable)
    {
        return new VaultLease
        {
            LeaseId = $"lease_{Guid.NewGuid():N}",
            SecretId = secretId,
            Ttl = ttlSeconds,
            Renewable = renewable,
            ExpiresAt = DateTime.UtcNow.AddSeconds(ttlSeconds)
        };
    }

    public void Renew(int incrementSeconds)
    {
        ExpiresAt = DateTime.UtcNow.AddSeconds(incrementSeconds);
        Ttl = incrementSeconds;
        SetUpdated();
    }

    public void Revoke()
    {
        Revoked = true;
        RevokedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
