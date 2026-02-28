using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class VaultSecret : BaseEntity
{
    public string Path { get; private set; } = string.Empty;
    public int Version { get; private set; } = 1;
    public string EncryptedData { get; private set; } = string.Empty;
    public string Iv { get; private set; } = string.Empty;
    public string? Metadata { get; private set; } // JSON
    public Guid? CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    // Lease fields
    public string? LeaseId { get; private set; }
    public int? LeaseTtl { get; private set; }
    public bool LeaseRenewable { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }

    private VaultSecret() { }

    public static VaultSecret Create(
        string path,
        string encryptedData,
        string iv,
        Guid? createdBy = null,
        string? metadata = null,
        int version = 1)
    {
        return new VaultSecret
        {
            Path = path,
            EncryptedData = encryptedData,
            Iv = iv,
            CreatedBy = createdBy,
            Metadata = metadata,
            Version = version
        };
    }

    public void UpdateData(string encryptedData, string iv, string? metadata = null)
    {
        EncryptedData = encryptedData;
        Iv = iv;
        if (metadata is not null)
            Metadata = metadata;
        SetUpdated();
    }

    public void SetLease(string leaseId, int ttlSeconds, bool renewable)
    {
        LeaseId = leaseId;
        LeaseTtl = ttlSeconds;
        LeaseRenewable = renewable;
        LeaseExpiresAt = DateTime.UtcNow.AddSeconds(ttlSeconds);
        SetUpdated();
    }

    public void SoftDelete()
    {
        DeletedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Restore()
    {
        DeletedAt = null;
        SetUpdated();
    }

    public VaultSecret CreateNextVersion(string encryptedData, string iv, string? metadata = null)
    {
        return new VaultSecret
        {
            Path = Path,
            Version = Version + 1,
            EncryptedData = encryptedData,
            Iv = iv,
            CreatedBy = CreatedBy,
            Metadata = metadata ?? Metadata
        };
    }
}
