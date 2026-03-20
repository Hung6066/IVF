using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class EggDonorConfiguration : IEntityTypeConfiguration<EggDonor>
{
    public void Configure(EntityTypeBuilder<EggDonor> builder)
    {
        builder.ToTable("egg_donors");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.DonorCode).IsRequired().HasMaxLength(20);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.BloodType).HasMaxLength(10);
        builder.Property(d => d.EyeColor).HasMaxLength(50);
        builder.Property(d => d.HairColor).HasMaxLength(50);
        builder.Property(d => d.Ethnicity).HasMaxLength(100);
        builder.Property(d => d.Education).HasMaxLength(200);
        builder.Property(d => d.Occupation).HasMaxLength(200);
        builder.Property(d => d.MenstrualHistory).HasMaxLength(1000);
        builder.Property(d => d.MedicalHistory).HasMaxLength(2000);
        builder.Property(d => d.Notes).HasMaxLength(2000);
        builder.HasOne(d => d.Patient).WithMany().HasForeignKey(d => d.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => d.DonorCode).IsUnique();
        builder.HasIndex(d => d.PatientId);
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}

public class OocyteSampleConfiguration : IEntityTypeConfiguration<OocyteSample>
{
    public void Configure(EntityTypeBuilder<OocyteSample> builder)
    {
        builder.ToTable("oocyte_samples");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SampleCode).IsRequired().HasMaxLength(30);
        builder.Property(s => s.Notes).HasMaxLength(2000);
        builder.HasOne(s => s.Donor).WithMany(d => d.OocyteSamples).HasForeignKey(s => s.DonorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.CryoLocation).WithMany().HasForeignKey(s => s.CryoLocationId);
        builder.HasIndex(s => s.SampleCode).IsUnique();
        builder.HasIndex(s => s.DonorId);
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
