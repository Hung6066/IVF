using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PermissionDefinitionConfiguration : IEntityTypeConfiguration<PermissionDefinition>
{
    public void Configure(EntityTypeBuilder<PermissionDefinition> builder)
    {
        builder.ToTable("permission_definitions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(p => p.Code).IsUnique();

        builder.Property(p => p.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.GroupCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.GroupDisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.GroupIcon)
            .HasMaxLength(20);

        builder.HasIndex(p => new { p.GroupSortOrder, p.SortOrder });

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
