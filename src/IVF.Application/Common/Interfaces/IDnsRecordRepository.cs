using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IDnsRecordRepository
{
    Task<DnsRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<DnsRecord>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<DnsRecord>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(DnsRecord record, CancellationToken ct = default);
    Task UpdateAsync(DnsRecord record, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
