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

public class CoupleConfiguration : IEntityTypeConfiguration<Couple>
{
    public void Configure(EntityTypeBuilder<Couple> builder)
    {
        builder.ToTable("couples");

        builder.HasKey(c => c.Id);

        builder.HasOne(c => c.Wife)
            .WithMany(p => p.AsWife)
            .HasForeignKey(c => c.WifeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Husband)
            .WithMany(p => p.AsHusband)
            .HasForeignKey(c => c.HusbandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.SpermDonor)
            .WithMany()
            .HasForeignKey(c => c.SpermDonorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

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

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(u => u.Username).IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(u => u.Department)
            .HasMaxLength(50);

        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}

public class QueueTicketConfiguration : IEntityTypeConfiguration<QueueTicket>
{
    public void Configure(EntityTypeBuilder<QueueTicket> builder)
    {
        builder.ToTable("queue_tickets");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.TicketNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(q => q.QueueType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(q => q.DepartmentCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(q => q.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(q => q.Patient)
            .WithMany(p => p.QueueTickets)
            .HasForeignKey(q => q.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Cycle)
            .WithMany()
            .HasForeignKey(q => q.CycleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.CalledByUser)
            .WithMany()
            .HasForeignKey(q => q.CalledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(q => new { q.DepartmentCode, q.IssuedAt });
    }
}

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

public class EmbryoConfiguration : IEntityTypeConfiguration<Embryo>
{
    public void Configure(EntityTypeBuilder<Embryo> builder)
    {
        builder.ToTable("embryos");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Grade)
            .HasConversion<string>()
            .HasMaxLength(5);

        builder.Property(e => e.Day)
            .HasConversion<string>()
            .HasMaxLength(5);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(e => e.Cycle)
            .WithMany(c => c.Embryos)
            .HasForeignKey(e => e.CycleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CryoLocation)
            .WithMany()
            .HasForeignKey(e => e.CryoLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CycleId);
    }
}

public class CryoLocationConfiguration : IEntityTypeConfiguration<CryoLocation>
{
    public void Configure(EntityTypeBuilder<CryoLocation> builder)
    {
        builder.ToTable("cryo_locations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Tank)
            .HasMaxLength(20);

        builder.Property(c => c.Canister)
            .HasMaxLength(20);

        builder.Property(c => c.Cane)
            .HasMaxLength(20);

        builder.Property(c => c.Goblet)
            .HasMaxLength(20);

        builder.Property(c => c.Straw)
            .HasMaxLength(20);

        builder.Property(c => c.SpecimenType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(c => new { c.Tank, c.Canister, c.Cane, c.Goblet, c.Straw }).IsUnique();
    }
}
