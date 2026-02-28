using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class VaultAutoUnsealConfiguration : IEntityTypeConfiguration<VaultAutoUnseal>
{
    public void Configure(EntityTypeBuilder<VaultAutoUnseal> builder)
    {
        builder.ToTable("vault_auto_unseal");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.WrappedKey).IsRequired();
        builder.Property(a => a.KeyVaultUrl).HasMaxLength(500).IsRequired();
        builder.Property(a => a.KeyName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.KeyVersion).HasMaxLength(100);
        builder.Property(a => a.Algorithm).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Iv).HasMaxLength(200);
    }
}
