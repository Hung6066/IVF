using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Used by dotnet ef migrations add/update commands.
/// </summary>
public class IvfDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IvfDbContext>
{
    public IvfDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IvfDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=ivf_db;Username=postgres;Password=postgres");
        return new IvfDbContext(optionsBuilder.Options);
    }
}
