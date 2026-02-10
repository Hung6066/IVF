using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class LinkedFieldSourceConfiguration : IEntityTypeConfiguration<LinkedFieldSource>
{
    public void Configure(EntityTypeBuilder<LinkedFieldSource> builder)
    {
        builder.ToTable("linked_field_sources");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TargetFieldId).IsRequired();
        builder.Property(e => e.SourceTemplateId).IsRequired();
        builder.Property(e => e.SourceFieldId).IsRequired();
        builder.Property(e => e.FlowType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(e => e.Priority).HasDefaultValue(0);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.Description).HasMaxLength(500);

        // Unique: one source per target field + source field combination
        builder.HasIndex(e => new { e.TargetFieldId, e.SourceFieldId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        // Query by target field
        builder.HasIndex(e => e.TargetFieldId);

        // Query by source template
        builder.HasIndex(e => e.SourceTemplateId);

        // Relationships
        builder.HasOne(e => e.TargetField)
            .WithMany()
            .HasForeignKey(e => e.TargetFieldId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.SourceTemplate)
            .WithMany()
            .HasForeignKey(e => e.SourceTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.SourceField)
            .WithMany()
            .HasForeignKey(e => e.SourceFieldId)
            .OnDelete(DeleteBehavior.Restrict);

        // Global query filter
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
