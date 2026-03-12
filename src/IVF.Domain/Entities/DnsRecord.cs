using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class DnsRecord : BaseEntity, ITenantEntity
{
    private DnsRecord() { }

    public static DnsRecord Create(
        Guid tenantId,
        DnsRecordType recordType,
        string name,
        string content,
        int ttlSeconds)
    {
        return new DnsRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecordType = recordType,
            Name = name,
            Content = content,
            TtlSeconds = ttlSeconds,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Primary key
    public Guid TenantId { get; private set; }

    // DNS Record details
    public DnsRecordType RecordType { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public int TtlSeconds { get; private set; }
    public bool IsActive { get; private set; } = true;

    // External reference (Cloudflare ID)
    public string? CloudflareId { get; private set; }

    public void UpdateContent(string newContent)
    {
        Content = newContent;
        SetUpdated();
    }

    public void SetCloudflareId(string cloudflareId)
    {
        CloudflareId = cloudflareId;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }

    public void SetTenantId(Guid tenantId)
    {
        TenantId = tenantId;
    }
}
