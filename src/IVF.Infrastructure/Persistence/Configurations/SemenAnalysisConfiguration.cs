using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class SemenAnalysisConfiguration : IEntityTypeConfiguration<SemenAnalysis>
{
    public void Configure(EntityTypeBuilder<SemenAnalysis> builder)
    {
        builder.ToTable("semen_analyses");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.AnalysisType).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.Volume).HasPrecision(10, 2);
        builder.Property(s => s.Ph).HasPrecision(4, 2);
        builder.Property(s => s.Concentration).HasPrecision(10, 2);
        builder.Property(s => s.TotalCount).HasPrecision(10, 2);
        builder.Property(s => s.ProgressiveMotility).HasPrecision(5, 2);
        builder.Property(s => s.NonProgressiveMotility).HasPrecision(5, 2);
        builder.Property(s => s.Immotile).HasPrecision(5, 2);
        builder.Property(s => s.NormalMorphology).HasPrecision(5, 2);
        builder.Property(s => s.Vitality).HasPrecision(5, 2);
        builder.Property(s => s.PostWashConcentration).HasPrecision(10, 2);
        builder.Property(s => s.PostWashMotility).HasPrecision(5, 2);

        builder.HasOne(s => s.Patient).WithMany().HasForeignKey(s => s.PatientId);
        builder.HasOne(s => s.Cycle).WithMany().HasForeignKey(s => s.CycleId);

        builder.HasIndex(s => s.PatientId);
        builder.HasIndex(s => s.CycleId);
        builder.HasIndex(s => s.AnalysisDate);
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
