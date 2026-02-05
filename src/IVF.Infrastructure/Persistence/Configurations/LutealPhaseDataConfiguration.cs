using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class LutealPhaseDataConfiguration : IEntityTypeConfiguration<LutealPhaseData>
{
    public void Configure(EntityTypeBuilder<LutealPhaseData> builder)
    {
        builder.ToTable("luteal_phase_data");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.LutealDrug1).HasMaxLength(100);
        builder.Property(t => t.LutealDrug2).HasMaxLength(100);
        builder.Property(t => t.EndometriumDrug1).HasMaxLength(100);
        builder.Property(t => t.EndometriumDrug2).HasMaxLength(100);

        builder.HasOne(t => t.Cycle)
            .WithOne(c => c.LutealPhase)
            .HasForeignKey<LutealPhaseData>(t => t.CycleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
