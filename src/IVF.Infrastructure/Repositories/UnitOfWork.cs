using IVF.Application.Common.Interfaces;
using IVF.Infrastructure.Persistence;

namespace IVF.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly IvfDbContext _context;

    public UnitOfWork(IvfDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
