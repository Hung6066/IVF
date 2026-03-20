using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class LabOrderConfiguration : IEntityTypeConfiguration<LabOrder>
{
    public void Configure(EntityTypeBuilder<LabOrder> builder)
    {
        builder.ToTable("lab_orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderType).IsRequired().HasMaxLength(30);
        builder.Property(o => o.Status).IsRequired().HasMaxLength(20);
        builder.Property(o => o.ResultDeliveredTo).HasMaxLength(20);
        builder.Property(o => o.Notes).HasMaxLength(1000);

        builder.HasOne(o => o.Patient).WithMany().HasForeignKey(o => o.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Cycle).WithMany().HasForeignKey(o => o.CycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.OrderedBy).WithMany().HasForeignKey(o => o.OrderedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.PatientId);
        builder.HasIndex(o => o.CycleId);
        builder.HasIndex(o => o.TenantId);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.OrderedAt);
        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}

public class LabTestConfiguration : IEntityTypeConfiguration<LabTest>
{
    public void Configure(EntityTypeBuilder<LabTest> builder)
    {
        builder.ToTable("lab_tests");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TestCode).IsRequired().HasMaxLength(30);
        builder.Property(t => t.TestName).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ResultValue).HasMaxLength(200);
        builder.Property(t => t.ResultUnit).HasMaxLength(50);
        builder.Property(t => t.ReferenceRange).HasMaxLength(100);
        builder.Property(t => t.Notes).HasMaxLength(500);

        builder.HasOne(t => t.LabOrder).WithMany(o => o.Tests).HasForeignKey(t => t.LabOrderId);

        builder.HasIndex(t => t.LabOrderId);
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
