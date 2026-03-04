using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("tenant_subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Plan)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.BillingCycle)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(s => s.DiscountPercent).HasPrecision(5, 2);
        builder.Property(s => s.Currency).HasMaxLength(3);

        builder.HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.TenantId);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
