using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class MedicationAdministrationConfiguration : IEntityTypeConfiguration<MedicationAdministration>
{
    public void Configure(EntityTypeBuilder<MedicationAdministration> builder)
    {
        builder.ToTable("medication_administrations");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.MedicationName).IsRequired().HasMaxLength(200);
        builder.Property(m => m.MedicationCode).HasMaxLength(50);
        builder.Property(m => m.Dosage).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Route).IsRequired().HasMaxLength(20);
        builder.Property(m => m.Site).HasMaxLength(100);
        builder.Property(m => m.BatchNumber).HasMaxLength(100);
        builder.Property(m => m.Notes).HasMaxLength(2000);
        builder.Property(m => m.Status).IsRequired().HasMaxLength(20);

        builder.HasOne(m => m.Patient).WithMany().HasForeignKey(m => m.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Cycle).WithMany().HasForeignKey(m => m.CycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Prescription).WithMany().HasForeignKey(m => m.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.AdministeredBy).WithMany().HasForeignKey(m => m.AdministeredByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.PatientId);
        builder.HasIndex(m => m.CycleId);
        builder.HasIndex(m => m.TenantId);
        builder.HasIndex(m => m.AdministeredAt);
        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
