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
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(f => new { f.FormTemplateId, f.FieldKey }).IsUnique();

        builder.Property(f => f.Label)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(f => f.Placeholder)
            .HasMaxLength(500);

        builder.Property(f => f.FieldType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(f => f.ValidationRulesJson)
            .HasColumnType("jsonb");

        builder.Property(f => f.OptionsJson)
            .HasColumnType("jsonb");

        builder.Property(f => f.DefaultValue)
            .HasMaxLength(2000);

        builder.Property(f => f.HelpText)
            .HasMaxLength(1000);

        builder.Property(f => f.ConditionalLogicJson)
            .HasColumnType("jsonb");

        builder.HasOne(f => f.FormTemplate)
            .WithMany(t => t.Fields)
            .HasForeignKey(f => f.FormTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.FieldValues)
            .WithOne(v => v.FormField)
            .HasForeignKey(v => v.FormFieldId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(f => !f.IsDeleted);
    }
}
