using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class TenantUsageRecordConfiguration : IEntityTypeConfiguration<TenantUsageRecord>
{
    public void Configure(EntityTypeBuilder<TenantUsageRecord> builder)
    {
        builder.ToTable("tenant_usage_records");

        builder.HasKey(u => u.Id);

        builder.HasOne(u => u.Tenant)
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(u => new { u.TenantId, u.Year, u.Month }).IsUnique();

        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
