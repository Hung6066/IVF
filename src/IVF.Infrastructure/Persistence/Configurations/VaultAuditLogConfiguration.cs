using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class VaultAuditLogConfiguration : IEntityTypeConfiguration<VaultAuditLog>
{
    public void Configure(EntityTypeBuilder<VaultAuditLog> builder)
    {
        builder.ToTable("vault_audit_logs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(200).IsRequired();
        builder.Property(a => a.ResourceType).HasMaxLength(100);
        builder.Property(a => a.ResourceId).HasMaxLength(200);
        builder.Property(a => a.Details).HasColumnType("jsonb");
        builder.Property(a => a.IpAddress).HasMaxLength(50);
        builder.Property(a => a.UserAgent).HasMaxLength(500);

        builder.HasIndex(a => a.Action);
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UserId);
    }
}
