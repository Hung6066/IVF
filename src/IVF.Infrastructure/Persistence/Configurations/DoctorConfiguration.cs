using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("doctors");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Specialty)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.LicenseNumber)
            .HasMaxLength(50);

        builder.Property(d => d.RoomNumber)
            .HasMaxLength(20);

        builder.Property(d => d.Schedule)
            .HasColumnType("jsonb");

        builder.HasOne(d => d.User)
            .WithOne()
            .HasForeignKey<Doctor>(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.UserId).IsUnique();
        builder.HasIndex(d => d.Specialty);
        builder.HasIndex(d => d.IsAvailable);
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
