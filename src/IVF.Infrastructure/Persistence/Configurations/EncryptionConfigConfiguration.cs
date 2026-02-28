using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class EncryptionConfigConfiguration : IEntityTypeConfiguration<EncryptionConfig>
{
    public void Configure(EntityTypeBuilder<EncryptionConfig> builder)
    {
        builder.ToTable("encryption_configs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TableName).HasMaxLength(200).IsRequired();
        builder.HasIndex(e => e.TableName).IsUnique();

        builder.Property(e => e.EncryptedFields)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(e => e.DekPurpose).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
    }
}
