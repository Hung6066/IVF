using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class VaultDynamicCredentialConfiguration : IEntityTypeConfiguration<VaultDynamicCredential>
{
    public void Configure(EntityTypeBuilder<VaultDynamicCredential> builder)
    {
        builder.ToTable("vault_dynamic_credentials");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.LeaseId).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Backend).HasMaxLength(50).IsRequired();
        builder.Property(d => d.Username).HasMaxLength(200).IsRequired();
        builder.Property(d => d.DbHost).HasMaxLength(500).IsRequired();
        builder.Property(d => d.DbName).HasMaxLength(200).IsRequired();
        builder.Property(d => d.AdminUsername).HasMaxLength(200).IsRequired();
        builder.Property(d => d.AdminPasswordEncrypted).IsRequired();

        builder.HasIndex(d => d.LeaseId).IsUnique();
        builder.HasIndex(d => d.ExpiresAt);
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
