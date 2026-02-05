using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class BirthDataConfiguration : IEntityTypeConfiguration<BirthData>
{
    public void Configure(EntityTypeBuilder<BirthData> builder)
    {
        builder.ToTable("birth_data");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.DeliveryMethod).HasMaxLength(50);
        builder.Property(t => t.BabyGenders).HasMaxLength(50);
        builder.Property(t => t.BirthWeights).HasMaxLength(100);
        builder.Property(t => t.Complications).HasMaxLength(2000);

        builder.HasOne(t => t.Cycle)
            .WithOne(c => c.Birth)
            .HasForeignKey<BirthData>(t => t.CycleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
