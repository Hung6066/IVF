using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class VaultTokenConfiguration : IEntityTypeConfiguration<VaultToken>
{
    public void Configure(EntityTypeBuilder<VaultToken> builder)
    {
        builder.ToTable("vault_tokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Accessor).HasMaxLength(200).IsRequired();
        builder.Property(t => t.TokenHash).HasMaxLength(500).IsRequired();
        builder.Property(t => t.DisplayName).HasMaxLength(200);
        builder.Property(t => t.TokenType).HasMaxLength(50).IsRequired();
        builder.Property(t => t.MetadataJson).HasColumnType("jsonb");

        builder.HasIndex(t => t.Accessor).IsUnique();
        builder.HasIndex(t => t.ExpiresAt);
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
