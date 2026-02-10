using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class LutealPhaseDrugConfiguration : IEntityTypeConfiguration<LutealPhaseDrug>
{
    public void Configure(EntityTypeBuilder<LutealPhaseDrug> builder)
    {
        builder.ToTable("luteal_phase_drugs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.DrugName).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Category).HasMaxLength(20).IsRequired();

        builder.HasOne(l => l.LutealPhaseData)
            .WithMany(d => d.Drugs)
            .HasForeignKey(l => l.LutealPhaseDataId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.LutealPhaseDataId);
        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}
