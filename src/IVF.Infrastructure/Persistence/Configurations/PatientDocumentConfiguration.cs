using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PatientDocumentConfiguration : IEntityTypeConfiguration<PatientDocument>
{
    public void Configure(EntityTypeBuilder<PatientDocument> builder)
    {
        builder.ToTable("patient_documents");

        builder.HasKey(d => d.Id);

        // ─── Patient relationship ───
        builder.HasOne(d => d.Patient)
            .WithMany()
            .HasForeignKey(d => d.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.PatientId);

        // ─── Version chain ───
        builder.HasOne(d => d.PreviousVersion)
            .WithMany()
            .HasForeignKey(d => d.PreviousVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        // ─── Document metadata ───
        builder.Property(d => d.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.Description)
            .HasMaxLength(2000);

        builder.Property(d => d.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(d => d.DocumentType);

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.Confidentiality)
            .HasConversion<string>()
            .HasMaxLength(20);

        // ─── MinIO storage info ───
        builder.Property(d => d.BucketName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.ObjectKey)
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasIndex(d => new { d.BucketName, d.ObjectKey }).IsUnique();

        builder.Property(d => d.OriginalFileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.Checksum)
            .HasMaxLength(128);

        // ─── Signing info ───
        builder.Property(d => d.SignedByName)
            .HasMaxLength(200);

        builder.Property(d => d.SignedObjectKey)
            .HasMaxLength(1000);

        // ─── Tags & metadata ───
        builder.Property(d => d.Tags)
            .HasColumnType("jsonb");

        builder.Property(d => d.MetadataJson)
            .HasColumnType("jsonb");

        // ─── Indexes for common queries ───
        builder.HasIndex(d => new { d.PatientId, d.DocumentType, d.Status });
        builder.HasIndex(d => d.CreatedAt);
        builder.HasIndex(d => d.IsSigned);

        // ─── Soft delete filter ───
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
