using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class FormFieldOptionConfiguration : IEntityTypeConfiguration<FormFieldOption>
{
    public void Configure(EntityTypeBuilder<FormFieldOption> builder)
    {
        builder.ToTable("form_field_options");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Value)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(o => o.Label)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(o => o.DisplayOrder)
            .IsRequired();

        // Relationship to FormField
        builder.HasOne(o => o.FormField)
            .WithMany(f => f.Options)
            .HasForeignKey(o => o.FormFieldId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship to Concept
        builder.HasOne(o => o.Concept)
            .WithMany(c => c.FormFieldOptions)
            .HasForeignKey(o => o.ConceptId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(o => new { o.FormFieldId, o.DisplayOrder });

        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}
