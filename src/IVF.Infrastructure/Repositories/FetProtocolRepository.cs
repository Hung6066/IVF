using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class FetProtocolRepository(IvfDbContext context) : IFetProtocolRepository
{
    public async Task<FetProtocol?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.FetProtocols
            .Include(p => p.Cycle)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<FetProtocol?> GetByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await context.FetProtocols
            .Include(p => p.Cycle)
            .FirstOrDefaultAsync(p => p.CycleId == cycleId, ct);

    public async Task<IReadOnlyList<FetProtocol>> SearchAsync(string? query, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = context.FetProtocols
            .Include(p => p.Cycle)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(p => p.Status == status);

        q = q.OrderByDescending(p => p.CreatedAt);
        return await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    }

    public async Task<int> CountAsync(string? query, string? status, CancellationToken ct = default)
    {
        var q = context.FetProtocols.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(p => p.Status == status);
        return await q.CountAsync(ct);
    }

    public async Task<FetProtocol> AddAsync(FetProtocol protocol, CancellationToken ct = default)
    {
        await context.FetProtocols.AddAsync(protocol, ct);
        return protocol;
    }

    public Task UpdateAsync(FetProtocol protocol, CancellationToken ct = default)
    {
        context.FetProtocols.Update(protocol);
        return Task.CompletedTask;
    }
}
