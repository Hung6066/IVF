using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class CultureDataConfiguration : IEntityTypeConfiguration<CultureData>
{
    public void Configure(EntityTypeBuilder<CultureData> builder)
    {
        builder.ToTable("culture_data");
        builder.HasKey(t => t.Id);

        builder.HasOne(t => t.Cycle)
            .WithOne(c => c.Culture)
            .HasForeignKey<CultureData>(t => t.CycleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.CycleId);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
