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

        builder.HasIndex(p => p.FullName);

        builder.Property(p => p.Phone)
            .HasMaxLength(20);

        builder.Property(p => p.Email)
            .HasMaxLength(200);

        builder.Property(p => p.Address)
            .HasMaxLength(500);

        builder.Property(p => p.Gender)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(p => p.PatientType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(IVF.Domain.Enums.PatientStatus.Active);

        // Demographics
        builder.Property(p => p.Ethnicity).HasMaxLength(100);
        builder.Property(p => p.Nationality).HasMaxLength(100);
        builder.Property(p => p.Occupation).HasMaxLength(200);
        builder.Property(p => p.InsuranceNumber).HasMaxLength(50);
        builder.Property(p => p.InsuranceProvider).HasMaxLength(200);
        builder.Property(p => p.BloodType).HasConversion<string>().HasMaxLength(15);
        builder.Property(p => p.Allergies).HasMaxLength(1000);

        // Emergency contact
        builder.Property(p => p.EmergencyContactName).HasMaxLength(200);
        builder.Property(p => p.EmergencyContactPhone).HasMaxLength(20);
        builder.Property(p => p.EmergencyContactRelation).HasMaxLength(100);

        // Referral
        builder.Property(p => p.ReferralSource).HasMaxLength(200);
        builder.Property(p => p.MedicalNotes).HasMaxLength(4000);

        // Risk & priority
        builder.Property(p => p.RiskLevel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(IVF.Domain.Enums.RiskLevel.Low);

        builder.Property(p => p.RiskNotes).HasMaxLength(1000);

        builder.Property(p => p.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(IVF.Domain.Enums.PatientPriority.Normal);

        // Tags & notes
        builder.Property(p => p.Tags).HasMaxLength(1000);
        builder.Property(p => p.Notes).HasMaxLength(4000);

        // Composite indexes for common queries
        builder.HasIndex(p => p.Phone);
        builder.HasIndex(p => p.Email);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.PatientType);
        builder.HasIndex(p => p.RiskLevel);
        builder.HasIndex(p => p.Priority);
        builder.HasIndex(p => p.LastVisitDate);
        builder.HasIndex(p => p.CreatedAt);
        builder.HasIndex(p => new { p.Status, p.LastVisitDate });
        builder.HasIndex(p => new { p.DataRetentionExpiryDate, p.IsAnonymized });
        builder.HasIndex(p => p.InsuranceNumber);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
