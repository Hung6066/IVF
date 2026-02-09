using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class FormFieldValueConfiguration : IEntityTypeConfiguration<FormFieldValue>
{
    public void Configure(EntityTypeBuilder<FormFieldValue> builder)
    {
        builder.ToTable("form_field_values");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.TextValue)
            .HasMaxLength(4000);

        builder.Property(v => v.NumericValue)
            .HasPrecision(18, 6);

        builder.Property(v => v.JsonValue)
            .HasColumnType("text");  // Changed from jsonb to text to allow null/empty values

        builder.HasOne(v => v.FormResponse)
            .WithMany(r => r.FieldValues)
            .HasForeignKey(v => v.FormResponseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.FormField)
            .WithMany(f => f.FieldValues)
            .HasForeignKey(v => v.FormFieldId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.FormResponseId, v.FormFieldId }).IsUnique();

        builder.HasQueryFilter(v => !v.IsDeleted);
    }
}
