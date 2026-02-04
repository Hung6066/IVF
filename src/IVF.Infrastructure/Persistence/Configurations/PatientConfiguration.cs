using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patients");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PatientCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(p => p.PatientCode).IsUnique();

        builder.Property(p => p.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.IdentityNumber)
            .HasMaxLength(20);

        builder.HasIndex(p => p.IdentityNumber);

        builder.Property(p => p.Phone)
            .HasMaxLength(20);

        builder.Property(p => p.Address)
            .HasMaxLength(500);

        builder.Property(p => p.Gender)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(p => p.PatientType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
