using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class InventoryRequestConfiguration : IEntityTypeConfiguration<InventoryRequest>
{
    public void Configure(EntityTypeBuilder<InventoryRequest> builder)
    {
        builder.ToTable("inventory_requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequestType).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Unit).IsRequired().HasMaxLength(20);
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.RejectionReason).HasMaxLength(500);

        builder.HasOne(r => r.RequestedBy).WithMany().HasForeignKey(r => r.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ApprovedBy).WithMany().HasForeignKey(r => r.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.Status);
        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
