using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PlanDefinitionConfiguration : IEntityTypeConfiguration<PlanDefinition>
{
    public void Configure(EntityTypeBuilder<PlanDefinition> builder)
    {
        builder.ToTable("plan_definitions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Plan)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(p => p.Plan).IsUnique();

        builder.Property(p => p.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.MonthlyPrice)
            .HasPrecision(18, 2);

        builder.Property(p => p.Currency)
            .HasMaxLength(3)
            .HasDefaultValue("VND");

        builder.Property(p => p.Duration)
            .HasMaxLength(50);

        builder.HasMany(p => p.PlanFeatures)
            .WithOne(pf => pf.PlanDefinition)
            .HasForeignKey(pf => pf.PlanDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
