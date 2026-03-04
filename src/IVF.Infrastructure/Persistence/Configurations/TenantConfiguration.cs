using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Slug)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.LogoUrl).HasMaxLength(500);
        builder.Property(t => t.Address).HasMaxLength(500);
        builder.Property(t => t.Phone).HasMaxLength(20);
        builder.Property(t => t.Email).HasMaxLength(200);
        builder.Property(t => t.TaxId).HasMaxLength(50);
        builder.Property(t => t.Website).HasMaxLength(200);
        builder.Property(t => t.PrimaryColor).HasMaxLength(20);
        builder.Property(t => t.Locale).HasMaxLength(10);
        builder.Property(t => t.TimeZone).HasMaxLength(50);
        builder.Property(t => t.CustomDomain).HasMaxLength(200);
        builder.Property(t => t.ConnectionString).HasMaxLength(500);
        builder.Property(t => t.DatabaseSchema).HasMaxLength(63);

        builder.Property(t => t.IsolationStrategy)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(IVF.Domain.Enums.DataIsolationStrategy.SharedDatabase);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
