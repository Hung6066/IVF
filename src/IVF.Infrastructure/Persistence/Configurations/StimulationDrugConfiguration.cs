using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class StimulationDrugConfiguration : IEntityTypeConfiguration<StimulationDrug>
{
    public void Configure(EntityTypeBuilder<StimulationDrug> builder)
    {
        builder.ToTable("stimulation_drugs");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.DrugName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Posology).HasMaxLength(50);

        builder.HasOne(s => s.StimulationData)
            .WithMany(d => d.Drugs)
            .HasForeignKey(s => s.StimulationDataId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.StimulationDataId);
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
