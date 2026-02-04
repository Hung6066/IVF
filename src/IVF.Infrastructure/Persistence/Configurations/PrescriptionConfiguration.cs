using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("prescriptions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status).HasMaxLength(20);
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.HasOne(p => p.Patient).WithMany().HasForeignKey(p => p.PatientId);
        builder.HasOne(p => p.Cycle).WithMany().HasForeignKey(p => p.CycleId);
        builder.HasOne(p => p.Doctor).WithMany().HasForeignKey(p => p.DoctorId);

        builder.HasIndex(p => p.PatientId);
        builder.HasIndex(p => p.PrescriptionDate);
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
