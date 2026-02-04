using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PaymentNumber).IsRequired().HasMaxLength(30);
        builder.Property(p => p.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.TransactionReference).HasMaxLength(100);

        builder.HasOne(p => p.Invoice).WithMany(i => i.Payments).HasForeignKey(p => p.InvoiceId);

        builder.HasIndex(p => p.PaymentNumber).IsUnique();
        builder.HasIndex(p => p.InvoiceId);
        builder.HasIndex(p => p.PaymentDate);
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
