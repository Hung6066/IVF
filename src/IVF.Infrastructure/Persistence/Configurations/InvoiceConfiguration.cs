using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(30);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.SubTotal).HasPrecision(18, 2);
        builder.Property(i => i.DiscountPercent).HasPrecision(5, 2);
        builder.Property(i => i.DiscountAmount).HasPrecision(18, 2);
        builder.Property(i => i.TaxPercent).HasPrecision(5, 2);
        builder.Property(i => i.TaxAmount).HasPrecision(18, 2);
        builder.Property(i => i.TotalAmount).HasPrecision(18, 2);
        builder.Property(i => i.PaidAmount).HasPrecision(18, 2);

        builder.HasOne(i => i.Patient).WithMany().HasForeignKey(i => i.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Cycle).WithMany().HasForeignKey(i => i.CycleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.HasIndex(i => i.PatientId);
        builder.HasIndex(i => i.CycleId);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.InvoiceDate);
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
