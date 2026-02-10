using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class FormFieldConfiguration : IEntityTypeConfiguration<FormField>
{
    public void Configure(EntityTypeBuilder<FormField> builder)
    {
        builder.ToTable("form_fields");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.FieldKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.Label)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(f => f.Placeholder)
            .HasMaxLength(300);

        builder.Property(f => f.HelpText)
            .HasMaxLength(1000);

        builder.Property(f => f.FieldType)
            .HasConversion<string>()
            .HasMaxLength(30);

        // Relationships
        builder.HasOne(f => f.FormTemplate)
            .WithMany(t => t.Fields)
            .HasForeignKey(f => f.FormTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Concept)
            .WithMany(c => c.FormFields)
            .HasForeignKey(f => f.ConceptId)
            .OnDelete(DeleteBehavior.SetNull);

        // Options & FieldValues relationships configured in their own Configuration files

        // Indexes
        builder.HasIndex(f => f.FormTemplateId);
        builder.HasIndex(f => new { f.FormTemplateId, f.DisplayOrder });
        builder.HasIndex(f => f.ConceptId);

        // Soft delete filter
        builder.HasQueryFilter(f => !f.IsDeleted);
    }
}
