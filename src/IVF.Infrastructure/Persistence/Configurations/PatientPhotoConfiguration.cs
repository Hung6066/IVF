using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PatientPhotoConfiguration : IEntityTypeConfiguration<PatientPhoto>
{
    public void Configure(EntityTypeBuilder<PatientPhoto> builder)
    {
        builder.ToTable("PatientPhotos");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PatientId)
            .IsRequired();

        builder.Property(p => p.PhotoData)
            .IsRequired();

        builder.Property(p => p.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.FileName)
            .HasMaxLength(255);

        builder.Property(p => p.UploadedAt)
            .IsRequired();

        // 1:1 relationship with Patient
        builder.HasIndex(p => p.PatientId)
            .IsUnique();

        builder.HasOne(p => p.Patient)
            .WithOne(p => p.Photo)
            .HasForeignKey<PatientPhoto>(p => p.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
