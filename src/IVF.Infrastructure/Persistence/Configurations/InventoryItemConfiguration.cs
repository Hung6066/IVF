using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("inventory_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Code).IsRequired().HasMaxLength(50);
        builder.Property(i => i.Name).IsRequired().HasMaxLength(200);
        builder.Property(i => i.GenericName).HasMaxLength(200);
        builder.Property(i => i.Category).IsRequired().HasMaxLength(50);
        builder.Property(i => i.Unit).IsRequired().HasMaxLength(20);
        builder.Property(i => i.Manufacturer).HasMaxLength(200);
        builder.Property(i => i.Supplier).HasMaxLength(200);
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        builder.Property(i => i.BatchNumber).HasMaxLength(100);
        builder.Property(i => i.StorageLocation).HasMaxLength(100);
        builder.Property(i => i.Notes).HasMaxLength(2000);
        builder.HasIndex(i => i.Code).IsUnique();
        builder.HasIndex(i => i.TenantId);
        builder.HasIndex(i => i.Category);
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}

public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> builder)
    {
        builder.ToTable("stock_transactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TransactionType).IsRequired().HasMaxLength(20);
        builder.Property(t => t.Reference).HasMaxLength(200);
        builder.Property(t => t.Reason).HasMaxLength(500);
        builder.Property(t => t.PerformedByName).HasMaxLength(100);
        builder.Property(t => t.SupplierName).HasMaxLength(200);
        builder.Property(t => t.UnitCost).HasPrecision(18, 2);
        builder.Property(t => t.BatchNumber).HasMaxLength(100);
        builder.HasOne(t => t.Item).WithMany(i => i.Transactions).HasForeignKey(t => t.ItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => t.ItemId);
        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => t.CreatedAt);
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
