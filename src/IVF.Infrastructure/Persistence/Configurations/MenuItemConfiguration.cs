using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("menu_items");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Section)
            .HasMaxLength(50);

        builder.Property(m => m.SectionHeader)
            .HasMaxLength(100);

        builder.Property(m => m.Icon)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Label)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.Route)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Permission)
            .HasMaxLength(100);

        builder.HasIndex(m => new { m.Section, m.SortOrder });
        builder.HasIndex(m => m.Route).IsUnique();

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
