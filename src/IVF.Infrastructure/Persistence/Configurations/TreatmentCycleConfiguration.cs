using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class TreatmentCycleConfiguration : IEntityTypeConfiguration<TreatmentCycle>
{
    public void Configure(EntityTypeBuilder<TreatmentCycle> builder)
    {
        builder.ToTable("treatment_cycles");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.CycleCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(t => t.CycleCode).IsUnique();

        builder.Property(t => t.Method)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(t => t.CurrentPhase)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(t => t.Outcome)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(t => t.Couple)
            .WithMany(c => c.TreatmentCycles)
            .HasForeignKey(t => t.CoupleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.CoupleId);
        builder.HasIndex(t => t.StartDate);
        builder.HasIndex(t => t.CurrentPhase);

        // Extended fields
        builder.Property(t => t.Room).HasMaxLength(50);
        builder.Property(t => t.EtIuiDoctor).HasMaxLength(100);
        builder.Property(t => t.CtrlNote).HasMaxLength(500);
        builder.Property(t => t.StopReason).HasMaxLength(500);
        builder.Property(t => t.BetaHcg).HasPrecision(10, 2);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
