namespace IVF.Application.Common.Interfaces;

public interface ICaddyConfigService
{
    Task<string> GenerateCaddyfileAsync(CancellationToken ct = default);
    Task<CaddySyncResult> SyncConfigAsync(CancellationToken ct = default);
    Task<string?> GetCurrentConfigAsync(CancellationToken ct = default);
    Task<List<TenantDomainInfo>> GetTenantDomainsAsync(CancellationToken ct = default);
}

public record CaddySyncResult(bool Success, string Message, int DomainsConfigured = 0);

public record TenantDomainInfo(
    Guid TenantId,
    string TenantName,
    string Slug,
    string? Subdomain,
    string? CustomDomain,
    string CustomDomainStatus,
    bool IsActive);
