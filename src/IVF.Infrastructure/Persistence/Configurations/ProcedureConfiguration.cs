using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class ProcedureConfiguration : IEntityTypeConfiguration<Procedure>
{
    public void Configure(EntityTypeBuilder<Procedure> builder)
    {
        builder.ToTable("procedures");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProcedureType).HasMaxLength(100).IsRequired();
        builder.Property(p => p.ProcedureCode).HasMaxLength(50);
        builder.Property(p => p.ProcedureName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Status).HasMaxLength(50).IsRequired();
        builder.Property(p => p.AnesthesiaType).HasMaxLength(100);
        builder.Property(p => p.AnesthesiaNotes).HasMaxLength(1000);
        builder.Property(p => p.PreOpNotes).HasMaxLength(2000);
        builder.Property(p => p.IntraOpFindings).HasMaxLength(2000);
        builder.Property(p => p.PostOpNotes).HasMaxLength(2000);
        builder.Property(p => p.Complications).HasMaxLength(2000);
        builder.Property(p => p.RoomNumber).HasMaxLength(50);

        builder.HasOne(p => p.Patient).WithMany().HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.PerformedByDoctor).WithMany().HasForeignKey(p => p.PerformedByDoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.AssistantDoctor).WithMany().HasForeignKey(p => p.AssistantDoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Cycle).WithMany().HasForeignKey(p => p.CycleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.PatientId);
        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.ScheduledAt);
        builder.HasIndex(p => p.PerformedByDoctorId);
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
