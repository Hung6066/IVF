using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class VaultPolicyConfiguration : IEntityTypeConfiguration<VaultPolicy>
{
    public void Configure(EntityTypeBuilder<VaultPolicy> builder)
    {
        builder.ToTable("vault_policies");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.PathPattern).HasMaxLength(500).IsRequired();

        builder.HasIndex(p => p.Name).IsUnique();
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
