using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class VaultLeaseConfiguration : IEntityTypeConfiguration<VaultLease>
{
    public void Configure(EntityTypeBuilder<VaultLease> builder)
    {
        builder.ToTable("vault_leases");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.LeaseId).HasMaxLength(200).IsRequired();

        builder.HasOne(l => l.Secret)
            .WithMany()
            .HasForeignKey(l => l.SecretId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.LeaseId).IsUnique();
        builder.HasIndex(l => l.ExpiresAt);
        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}
