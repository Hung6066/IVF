using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class CycleFeeConfiguration : IEntityTypeConfiguration<CycleFee>
{
    public void Configure(EntityTypeBuilder<CycleFee> builder)
    {
        builder.ToTable("cycle_fees");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FeeType).IsRequired().HasMaxLength(50);
        builder.Property(f => f.Description).IsRequired().HasMaxLength(200);
        builder.Property(f => f.Amount).HasColumnType("decimal(18,2)");
        builder.Property(f => f.PaidAmount).HasColumnType("decimal(18,2)");
        builder.Property(f => f.Status).IsRequired().HasMaxLength(20);
        builder.Property(f => f.WaivedReason).HasMaxLength(500);
        builder.Property(f => f.Notes).HasMaxLength(2000);

        builder.Ignore(f => f.BalanceDue);

        builder.HasOne(f => f.Cycle).WithMany().HasForeignKey(f => f.CycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.Patient).WithMany().HasForeignKey(f => f.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.Invoice).WithMany().HasForeignKey(f => f.InvoiceId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.CycleId);
        builder.HasIndex(f => f.PatientId);
        builder.HasIndex(f => f.TenantId);
        builder.HasQueryFilter(f => !f.IsDeleted);
    }
}
