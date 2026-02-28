using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class FieldAccessPolicyConfiguration : IEntityTypeConfiguration<FieldAccessPolicy>
{
    public void Configure(EntityTypeBuilder<FieldAccessPolicy> builder)
    {
        builder.ToTable("field_access_policies");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TableName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FieldName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Role).HasMaxLength(50).IsRequired();
        builder.Property(e => e.AccessLevel).HasMaxLength(20).IsRequired();
        builder.Property(e => e.MaskPattern).HasMaxLength(50);
        builder.Property(e => e.Description).HasMaxLength(500);

        builder.HasIndex(e => new { e.TableName, e.FieldName, e.Role }).IsUnique();
    }
}
