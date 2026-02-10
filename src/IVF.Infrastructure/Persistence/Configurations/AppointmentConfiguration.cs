using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.Notes)
            .HasMaxLength(1000);

        builder.Property(a => a.RoomNumber)
            .HasMaxLength(20);

        builder.HasOne(a => a.Patient)
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Cycle)
            .WithMany()
            .HasForeignKey(a => a.CycleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Doctor)
            .WithMany()
            .HasForeignKey(a => a.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.ScheduledAt);
        builder.HasIndex(a => a.PatientId);
        builder.HasIndex(a => a.DoctorId);
        builder.HasIndex(a => a.CycleId);
        builder.HasIndex(a => new { a.ScheduledAt, a.Status });
        builder.HasIndex(a => new { a.DoctorId, a.ScheduledAt });
        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
