using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class SignedDocumentAmendmentConfiguration : IEntityTypeConfiguration<SignedDocumentAmendment>
{
    public void Configure(EntityTypeBuilder<SignedDocumentAmendment> builder)
    {
        builder.ToTable("signed_document_amendments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(a => a.ReviewNotes)
            .HasMaxLength(1000);

        builder.Property(a => a.Status)
            .IsRequired();

        builder.Property(a => a.Version)
            .IsRequired();

        builder.Property(a => a.OldValuesSnapshot)
            .HasColumnType("jsonb");

        builder.Property(a => a.NewValuesSnapshot)
            .HasColumnType("jsonb");

        // FormResponse is range-partitioned — skip FK, treat as logical reference
        builder.Property(a => a.FormResponseId)
            .IsRequired();

        builder.HasOne(a => a.RequestedByUser)
            .WithMany()
            .HasForeignKey(a => a.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ReviewedByUser)
            .WithMany()
            .HasForeignKey(a => a.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.FieldChanges)
            .WithOne(fc => fc.Amendment)
            .HasForeignKey(fc => fc.AmendmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(a => a.FormResponseId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => new { a.FormResponseId, a.Version })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
