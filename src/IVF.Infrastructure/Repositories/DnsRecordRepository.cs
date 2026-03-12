using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class DnsRecordRepository : IDnsRecordRepository
{
    private readonly IvfDbContext _context;

    public DnsRecordRepository(IvfDbContext context)
    {
        _context = context;
    }

    public async Task<DnsRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.DnsRecords
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);
    }

    public async Task<List<DnsRecord>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _context.DnsRecords
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<DnsRecord>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.DnsRecords
            .Where(r => !r.IsDeleted)
            .ToListAsync(ct);
    }

    public async Task AddAsync(DnsRecord record, CancellationToken ct = default)
    {
        await _context.DnsRecords.AddAsync(record, ct);
    }

    public async Task UpdateAsync(DnsRecord record, CancellationToken ct = default)
    {
        _context.DnsRecords.Update(record);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var record = await GetByIdAsync(id, ct);
        if (record != null)
        {
            record.MarkAsDeleted();
            _context.DnsRecords.Update(record);
        }
    }
}
