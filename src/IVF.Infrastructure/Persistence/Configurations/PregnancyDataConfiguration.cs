using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PregnancyDataConfiguration : IEntityTypeConfiguration<PregnancyData>
{
    public void Configure(EntityTypeBuilder<PregnancyData> builder)
    {
        builder.ToTable("pregnancy_data");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.BetaHcg).HasPrecision(10, 2);
        builder.Property(t => t.Notes).HasMaxLength(2000);

        builder.HasOne(t => t.Cycle)
            .WithOne(c => c.Pregnancy)
            .HasForeignKey<PregnancyData>(t => t.CycleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.CycleId);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
