using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class UltrasoundConfiguration : IEntityTypeConfiguration<Ultrasound>
{
    public void Configure(EntityTypeBuilder<Ultrasound> builder)
    {
        builder.ToTable("ultrasounds");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.UltrasoundType)
            .HasMaxLength(30);

        builder.Property(u => u.EndometriumThickness)
            .HasPrecision(5, 2);

        builder.Property(u => u.LeftFollicles)
            .HasColumnType("jsonb");

        builder.Property(u => u.RightFollicles)
            .HasColumnType("jsonb");

        builder.HasOne(u => u.Cycle)
            .WithMany(c => c.Ultrasounds)
            .HasForeignKey(u => u.CycleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Doctor)
            .WithMany()
            .HasForeignKey(u => u.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
