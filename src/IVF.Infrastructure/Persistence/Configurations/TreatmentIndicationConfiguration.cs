using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class TreatmentIndicationConfiguration : IEntityTypeConfiguration<TreatmentIndication>
{
    public void Configure(EntityTypeBuilder<TreatmentIndication> builder)
    {
        builder.ToTable("treatment_indications");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TreatmentType).HasMaxLength(50);
        builder.Property(t => t.Regimen).HasMaxLength(50);
        builder.Property(t => t.WifeDiagnosis).HasMaxLength(200);
        builder.Property(t => t.WifeDiagnosis2).HasMaxLength(200);
        builder.Property(t => t.HusbandDiagnosis).HasMaxLength(200);
        builder.Property(t => t.HusbandDiagnosis2).HasMaxLength(200);
        builder.Property(t => t.SubType).HasMaxLength(50);
        builder.Property(t => t.ScientificResearch).HasMaxLength(200);
        builder.Property(t => t.Source).HasMaxLength(100);
        builder.Property(t => t.ProcedurePlace).HasMaxLength(100);
        builder.Property(t => t.StopReason).HasMaxLength(500);

        builder.HasOne(t => t.Cycle)
            .WithOne(c => c.Indication)
            .HasForeignKey<TreatmentIndication>(t => t.CycleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
