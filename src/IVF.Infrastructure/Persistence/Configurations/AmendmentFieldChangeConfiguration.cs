using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class AmendmentFieldChangeConfiguration : IEntityTypeConfiguration<AmendmentFieldChange>
{
    public void Configure(EntityTypeBuilder<AmendmentFieldChange> builder)
    {
        builder.ToTable("amendment_field_changes");

        builder.HasKey(fc => fc.Id);

        builder.Property(fc => fc.FieldKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(fc => fc.FieldLabel)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(fc => fc.ChangeType)
            .IsRequired();

        builder.Property(fc => fc.OldTextValue).HasMaxLength(10000);
        builder.Property(fc => fc.NewTextValue).HasMaxLength(10000);
        builder.Property(fc => fc.OldJsonValue).HasColumnType("jsonb");
        builder.Property(fc => fc.NewJsonValue).HasColumnType("jsonb");

        builder.HasIndex(fc => fc.AmendmentId);

        builder.HasQueryFilter(fc => !fc.IsDeleted);
    }
}
