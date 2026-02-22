using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class DocumentSignatureConfiguration : IEntityTypeConfiguration<DocumentSignature>
{
    public void Configure(EntityTypeBuilder<DocumentSignature> builder)
    {
        builder.ToTable("document_signatures");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.SignatureRole)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.Notes)
            .HasMaxLength(500);

        builder.Property(d => d.SignedAt)
            .IsRequired();

        // FormResponse is a range-partitioned table in PostgreSQL.
        // PostgreSQL does NOT support FK constraints referencing partitioned tables
        // unless the unique index includes the partition key. We skip the FK
        // and treat FormResponseId as a logical reference validated at application level.
        builder.Ignore(d => d.FormResponse);

        builder.Property(d => d.FormResponseId)
            .IsRequired();

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index: quickly look up all signatures for a form response
        builder.HasIndex(d => d.FormResponseId);

        // Unique: one user can only sign one role per form response
        builder.HasIndex(d => new { d.FormResponseId, d.UserId, d.SignatureRole })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
