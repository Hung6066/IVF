using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class TenantFeatureConfiguration : IEntityTypeConfiguration<TenantFeature>
{
    public void Configure(EntityTypeBuilder<TenantFeature> builder)
    {
        builder.ToTable("tenant_features");

        builder.HasKey(tf => tf.Id);

        builder.HasOne(tf => tf.Tenant)
            .WithMany()
            .HasForeignKey(tf => tf.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tf => tf.FeatureDefinition)
            .WithMany()
            .HasForeignKey(tf => tf.FeatureDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tf => new { tf.TenantId, tf.FeatureDefinitionId }).IsUnique();

        builder.HasQueryFilter(tf => !tf.IsDeleted);
    }
}
