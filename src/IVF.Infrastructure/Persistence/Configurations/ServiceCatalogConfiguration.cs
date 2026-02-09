using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class ServiceCatalogConfiguration : IEntityTypeConfiguration<ServiceCatalog>
{
    public void Configure(EntityTypeBuilder<ServiceCatalog> builder)
    {
        builder.ToTable("ServiceCatalogs");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Unit)
            .HasMaxLength(50)
            .HasDefaultValue("lần");

        builder.Property(s => s.Description)
            .HasMaxLength(500);

        builder.Property(s => s.UnitPrice)
            .HasPrecision(18, 0);

        builder.Property(s => s.Category)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Unique index on Code
        builder.HasIndex(s => s.Code).IsUnique();

        // Index for filtering
        builder.HasIndex(s => s.Category);
        builder.HasIndex(s => s.IsActive);
    }
}
