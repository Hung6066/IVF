using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PatientFingerprintConfiguration : IEntityTypeConfiguration<PatientFingerprint>
{
    public void Configure(EntityTypeBuilder<PatientFingerprint> builder)
    {
        builder.ToTable("patient_fingerprints");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PatientId)
            .IsRequired();

        builder.Property(p => p.FingerprintData)
            .IsRequired();

        builder.Property(p => p.FingerType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.SdkType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.Quality)
            .IsRequired();

        builder.Property(p => p.CapturedAt)
            .IsRequired();

        // Composite index for patient + finger type (unique per patient per finger)
        builder.HasIndex(p => new { p.PatientId, p.FingerType })
            .IsUnique();

        builder.HasOne(p => p.Patient)
            .WithMany(p => p.Fingerprints)
            .HasForeignKey(p => p.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
