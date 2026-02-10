using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class SpermDonorConfiguration : IEntityTypeConfiguration<SpermDonor>
{
    public void Configure(EntityTypeBuilder<SpermDonor> builder)
    {
        builder.ToTable("sperm_donors");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DonorCode).IsRequired().HasMaxLength(20);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.BloodType).HasMaxLength(10);
        builder.Property(d => d.Height).HasPrecision(5, 2);
        builder.Property(d => d.Weight).HasPrecision(5, 2);
        builder.Property(d => d.EyeColor).HasMaxLength(30);
        builder.Property(d => d.HairColor).HasMaxLength(30);
        builder.Property(d => d.Ethnicity).HasMaxLength(50);
        builder.Property(d => d.Education).HasMaxLength(100);
        builder.Property(d => d.Occupation).HasMaxLength(100);

        builder.HasOne(d => d.Patient).WithMany().HasForeignKey(d => d.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.DonorCode).IsUnique();
        builder.HasIndex(d => d.PatientId);
        builder.HasIndex(d => d.Status);
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
