using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class ConceptConfiguration : IEntityTypeConfiguration<Concept>
{
    public void Configure(EntityTypeBuilder<Concept> builder)
    {
        builder.ToTable("concepts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Display)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.Description)
            .HasMaxLength(2000);

        builder.Property(c => c.System)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue("LOCAL");

        builder.Property(c => c.ConceptType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // PostgreSQL Full-Text Search (TsVector)
        // Auto-indexes Code, Display, and Description for fast concept search
        builder.Property(c => c.SearchVector)
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                @"to_tsvector('english', 
                    coalesce(""Code"", '') || ' ' || 
                    coalesce(""Display"", '') || ' ' ||
                    coalesce(""Description"", '')
                )",
                stored: true);

        // GIN index for fast full-text search
        builder.HasIndex(c => c.SearchVector)
            .HasMethod("GIN");

        // Relationships
        builder.HasMany(c => c.Mappings)
            .WithOne(m => m.Concept)
            .HasForeignKey(m => m.ConceptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.FormFields)
            .WithOne(f => f.Concept)
            .HasForeignKey(f => f.ConceptId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.FormFieldOptions)
            .WithOne(o => o.Concept)
            .HasForeignKey(o => o.ConceptId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasIndex(c => new { c.System, c.Code });
        builder.HasIndex(c => c.ConceptType);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
