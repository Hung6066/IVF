using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class VaultSecretConfiguration : IEntityTypeConfiguration<VaultSecret>
{
    public void Configure(EntityTypeBuilder<VaultSecret> builder)
    {
        builder.ToTable("vault_secrets");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Path).HasMaxLength(500).IsRequired();
        builder.Property(s => s.EncryptedData).IsRequired();
        builder.Property(s => s.Iv).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Metadata).HasColumnType("jsonb");
        builder.Property(s => s.LeaseId).HasMaxLength(200);

        builder.HasIndex(s => new { s.Path, s.Version }).IsUnique();
        builder.HasIndex(s => s.Path);
        builder.HasIndex(s => s.LeaseId).HasFilter("\"LeaseId\" IS NOT NULL");

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
