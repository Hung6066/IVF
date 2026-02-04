using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("invoice_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ServiceCode).IsRequired().HasMaxLength(30);
        builder.Property(i => i.Description).IsRequired().HasMaxLength(500);
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        builder.Ignore(i => i.Amount); // Computed property

        builder.HasOne(i => i.Invoice).WithMany(inv => inv.Items).HasForeignKey(i => i.InvoiceId);

        builder.HasIndex(i => i.InvoiceId);
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
