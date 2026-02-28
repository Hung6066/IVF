using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class VaultDynamicCredential : BaseEntity
{
    public string LeaseId { get; private set; } = string.Empty;
    public string Backend { get; private set; } = "postgres"; // postgres|mysql|mssql|redis
    public string Username { get; private set; } = string.Empty;
    public string DbHost { get; private set; } = string.Empty;
    public int DbPort { get; private set; }
    public string DbName { get; private set; } = string.Empty;
    public string AdminUsername { get; private set; } = string.Empty;
    public string AdminPasswordEncrypted { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public bool Revoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }

    private VaultDynamicCredential() { }

    public static VaultDynamicCredential Create(
        string backend,
        string username,
        string dbHost,
        int dbPort,
        string dbName,
        string adminUsername,
        string adminPasswordEncrypted,
        int ttlSeconds,
        Guid? createdBy = null)
    {
        return new VaultDynamicCredential
        {
            LeaseId = $"dynlease_{Guid.NewGuid():N}",
            Backend = backend,
            Username = username,
            DbHost = dbHost,
            DbPort = dbPort,
            DbName = dbName,
            AdminUsername = adminUsername,
            AdminPasswordEncrypted = adminPasswordEncrypted,
            ExpiresAt = DateTime.UtcNow.AddSeconds(ttlSeconds),
            CreatedBy = createdBy
        };
    }

    public void Revoke()
    {
        Revoked = true;
        RevokedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
