namespace IVF.Application.Common.Interfaces;

public interface IDnsProvider
{
    /// <summary>
    /// Creates a new DNS record via the provider (e.g., Cloudflare)
    /// </summary>
    Task<DnsProviderRecord> CreateRecordAsync(
        string recordType,
        string name,
        string content,
        int ttl,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a DNS record via the provider
    /// </summary>
    Task DeleteRecordAsync(string recordId, CancellationToken ct = default);

    /// <summary>
    /// Lists all DNS records via the provider
    /// </summary>
    Task<List<DnsProviderRecord>> ListRecordsAsync(CancellationToken ct = default);
}

public class DnsProviderRecord
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Ttl { get; set; }
    public bool Proxied { get; set; }
}
