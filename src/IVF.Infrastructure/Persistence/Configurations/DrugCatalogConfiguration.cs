using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class DrugCatalogConfiguration : IEntityTypeConfiguration<DrugCatalog>
{
    public void Configure(EntityTypeBuilder<DrugCatalog> builder)
    {
        builder.ToTable("drug_catalog");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Code).IsRequired().HasMaxLength(20);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
        builder.Property(d => d.GenericName).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(d => d.Unit).IsRequired().HasMaxLength(20);
        builder.Property(d => d.ActiveIngredient).HasMaxLength(300);
        builder.Property(d => d.DefaultDosage).HasMaxLength(100);
        builder.Property(d => d.Notes).HasMaxLength(1000);

        builder.HasIndex(d => new { d.Code, d.TenantId }).IsUnique();
        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => d.Category);
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
