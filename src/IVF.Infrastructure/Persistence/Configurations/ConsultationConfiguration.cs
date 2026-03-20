using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class ConsultationConfiguration : IEntityTypeConfiguration<Consultation>
{
    public void Configure(EntityTypeBuilder<Consultation> builder)
    {
        builder.ToTable("consultations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ConsultationType).IsRequired().HasMaxLength(30);
        builder.Property(c => c.Status).IsRequired().HasMaxLength(20);
        builder.Property(c => c.ChiefComplaint).HasMaxLength(500);
        builder.Property(c => c.MedicalHistory).HasMaxLength(2000);
        builder.Property(c => c.PastHistory).HasMaxLength(2000);
        builder.Property(c => c.SurgicalHistory).HasMaxLength(2000);
        builder.Property(c => c.FamilyHistory).HasMaxLength(2000);
        builder.Property(c => c.ObstetricHistory).HasMaxLength(2000);
        builder.Property(c => c.MenstrualHistory).HasMaxLength(1000);
        builder.Property(c => c.PhysicalExamination).HasMaxLength(2000);
        builder.Property(c => c.Diagnosis).HasMaxLength(1000);
        builder.Property(c => c.TreatmentPlan).HasMaxLength(2000);
        builder.Property(c => c.Notes).HasMaxLength(2000);

        builder.HasOne(c => c.Patient).WithMany().HasForeignKey(c => c.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Doctor).WithMany().HasForeignKey(c => c.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Cycle).WithMany().HasForeignKey(c => c.CycleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.PatientId);
        builder.HasIndex(c => c.DoctorId);
        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => c.ConsultationDate);
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
