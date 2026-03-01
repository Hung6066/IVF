using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// Design-time DbContext factory for EF Core CLI migrations.
/// Only used by `dotnet ef migrations add/update` — not used at runtime.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IvfDbContext>
{
    public IvfDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IvfDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=ivf_db;Username=postgres;Password=postgres");

        return new IvfDbContext(optionsBuilder.Options);
    }
}
