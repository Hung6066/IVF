using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class VaultToken : BaseEntity
{
    public string Accessor { get; private set; } = string.Empty; // Safe identifier for logging
    public string TokenHash { get; private set; } = string.Empty; // SHA-256 hash
    public string? DisplayName { get; private set; }
    public string[] Policies { get; private set; } = [];
    public string TokenType { get; private set; } = "service"; // service|batch
    public int? Ttl { get; private set; } // seconds, null = no expiry
    public int? NumUses { get; private set; } // null = unlimited
    public int UsesCount { get; private set; }
    public Guid? ParentId { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public bool Revoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? MetadataJson { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? LastUsedAt { get; private set; }

    private VaultToken() { }

    public static VaultToken Create(
        string tokenHash,
        string? displayName = null,
        string[]? policies = null,
        string tokenType = "service",
        int? ttl = null,
        int? numUses = null,
        Guid? createdBy = null)
    {
        return new VaultToken
        {
            Accessor = $"accessor_{Guid.NewGuid():N}"[..21],
            TokenHash = tokenHash,
            DisplayName = displayName,
            Policies = policies ?? ["default"],
            TokenType = tokenType,
            Ttl = ttl,
            NumUses = numUses,
            ExpiresAt = ttl.HasValue ? DateTime.UtcNow.AddSeconds(ttl.Value) : null,
            CreatedBy = createdBy
        };
    }

    public void IncrementUse()
    {
        UsesCount++;
        LastUsedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Revoke()
    {
        Revoked = true;
        RevokedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt;
    public bool IsExhausted => NumUses.HasValue && UsesCount >= NumUses;
    public bool IsValid => !Revoked && !IsExpired && !IsExhausted;
}
