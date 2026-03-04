using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> builder)
    {
        builder.ToTable("plan_features");

        builder.HasKey(pf => pf.Id);

        builder.HasOne(pf => pf.PlanDefinition)
            .WithMany(p => p.PlanFeatures)
            .HasForeignKey(pf => pf.PlanDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pf => pf.FeatureDefinition)
            .WithMany()
            .HasForeignKey(pf => pf.FeatureDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pf => new { pf.PlanDefinitionId, pf.FeatureDefinitionId }).IsUnique();

        builder.HasQueryFilter(pf => !pf.IsDeleted);
    }
}
