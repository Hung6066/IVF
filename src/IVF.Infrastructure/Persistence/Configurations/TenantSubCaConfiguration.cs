using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class TenantSubCaConfiguration : IEntityTypeConfiguration<TenantSubCa>
{
    public void Configure(EntityTypeBuilder<TenantSubCa> builder)
    {
        builder.ToTable("tenant_sub_cas");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.EjbcaCaName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.EjbcaCertProfileName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.EjbcaEeProfileName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.WorkerNamePrefix)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.OrganizationName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<int>()
            .HasDefaultValue(TenantSubCaStatus.Active);

        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // One Sub-CA per tenant
        builder.HasIndex(t => t.TenantId).IsUnique();

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
