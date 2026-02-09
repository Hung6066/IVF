using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class FormFieldValueDetailConfiguration : IEntityTypeConfiguration<FormFieldValueDetail>
{
    public void Configure(EntityTypeBuilder<FormFieldValueDetail> builder)
    {
        builder.ToTable("FormFieldValueDetails");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Value)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.Label)
            .HasMaxLength(1000);

        builder.HasOne(d => d.FormFieldValue)
            .WithMany(p => p.Details)
            .HasForeignKey(d => d.FormFieldValueId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional relationship to Concept
        // assuming Concept entity exists and has Id
        builder.HasOne(d => d.Concept)
            .WithMany()
            .HasForeignKey(d => d.ConceptId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Add query filter to match parent entity
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
