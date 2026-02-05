using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class StimulationDataConfiguration : IEntityTypeConfiguration<StimulationData>
{
    public void Configure(EntityTypeBuilder<StimulationData> builder)
    {
        builder.ToTable("stimulation_data");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Drug1).HasMaxLength(100);
        builder.Property(t => t.Drug1Posology).HasMaxLength(50);
        builder.Property(t => t.Drug2).HasMaxLength(100);
        builder.Property(t => t.Drug2Posology).HasMaxLength(50);
        builder.Property(t => t.Drug3).HasMaxLength(100);
        builder.Property(t => t.Drug3Posology).HasMaxLength(50);
        builder.Property(t => t.Drug4).HasMaxLength(100);
        builder.Property(t => t.Drug4Posology).HasMaxLength(50);
        builder.Property(t => t.TriggerDrug).HasMaxLength(100);
        builder.Property(t => t.TriggerDrug2).HasMaxLength(100);
        builder.Property(t => t.ProcedureType).HasMaxLength(50);
        builder.Property(t => t.TechniqueWife).HasMaxLength(100);
        builder.Property(t => t.TechniqueHusband).HasMaxLength(100);
        builder.Property(t => t.EndometriumThickness).HasPrecision(5, 2);
        builder.Property(t => t.LhLab).HasPrecision(10, 2);
        builder.Property(t => t.E2Lab).HasPrecision(10, 2);
        builder.Property(t => t.P4Lab).HasPrecision(10, 2);

        builder.HasOne(t => t.Cycle)
            .WithOne(c => c.Stimulation)
            .HasForeignKey<StimulationData>(t => t.CycleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
