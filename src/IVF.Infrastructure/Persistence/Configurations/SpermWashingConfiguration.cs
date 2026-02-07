using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class SpermWashingConfiguration : IEntityTypeConfiguration<SpermWashing>
{
    public void Configure(EntityTypeBuilder<SpermWashing> builder)
    {
        builder.ToTable("sperm_washings");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Method).HasMaxLength(50).IsRequired();
        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(w => w.Notes).HasMaxLength(500);

        builder.Property(w => w.PreWashConcentration).HasPrecision(10, 2);
        builder.Property(w => w.PostWashConcentration).HasPrecision(10, 2);
        builder.Property(w => w.PostWashMotility).HasPrecision(5, 2);

        builder.HasOne(w => w.Patient).WithMany().HasForeignKey(w => w.PatientId);
        builder.HasOne(w => w.Cycle).WithMany().HasForeignKey(w => w.CycleId);

        builder.HasIndex(w => w.PatientId);
        builder.HasIndex(w => w.CycleId);
        builder.HasIndex(w => w.WashDate);
        builder.HasQueryFilter(w => !w.IsDeleted);
    }
}
