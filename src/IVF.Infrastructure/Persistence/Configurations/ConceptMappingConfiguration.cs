using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class ConceptMappingConfiguration : IEntityTypeConfiguration<ConceptMapping>
{
    public void Configure(EntityTypeBuilder<ConceptMapping> builder)
    {
        builder.ToTable("ConceptMappings");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TargetSystem)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.TargetCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.TargetDisplay)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(m => m.Relationship)
            .HasMaxLength(50);

        builder.Property(m => m.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Relationship
        builder.HasOne(m => m.Concept)
            .WithMany(c => c.Mappings)
            .HasForeignKey(m => m.ConceptId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for fast lookups
        builder.HasIndex(m => new { m.ConceptId, m.TargetSystem });
        builder.HasIndex(m => new { m.TargetSystem, m.TargetCode });
        builder.HasIndex(m => m.IsActive);
    }
}
