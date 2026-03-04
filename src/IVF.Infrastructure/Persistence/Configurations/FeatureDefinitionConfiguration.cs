using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class FeatureDefinitionConfiguration : IEntityTypeConfiguration<FeatureDefinition>
{
    public void Configure(EntityTypeBuilder<FeatureDefinition> builder)
    {
        builder.ToTable("feature_definitions");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(f => f.Code).IsUnique();

        builder.Property(f => f.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.Description)
            .HasMaxLength(500);

        builder.Property(f => f.Icon)
            .HasMaxLength(20);

        builder.Property(f => f.Category)
            .HasMaxLength(50)
            .HasDefaultValue("core");

        builder.HasQueryFilter(f => !f.IsDeleted);
    }
}
