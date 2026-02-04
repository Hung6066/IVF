using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class SpermSampleConfiguration : IEntityTypeConfiguration<SpermSample>
{
    public void Configure(EntityTypeBuilder<SpermSample> builder)
    {
        builder.ToTable("sperm_samples");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SampleCode).IsRequired().HasMaxLength(30);
        builder.Property(s => s.SpecimenType).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Volume).HasPrecision(10, 2);
        builder.Property(s => s.Concentration).HasPrecision(10, 2);
        builder.Property(s => s.Motility).HasPrecision(5, 2);

        builder.HasOne(s => s.Donor).WithMany(d => d.SpermSamples).HasForeignKey(s => s.DonorId);
        builder.HasOne(s => s.CryoLocation).WithMany().HasForeignKey(s => s.CryoLocationId);

        builder.HasIndex(s => s.SampleCode).IsUnique();
        builder.HasIndex(s => s.DonorId);
        builder.HasIndex(s => s.IsAvailable);
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
