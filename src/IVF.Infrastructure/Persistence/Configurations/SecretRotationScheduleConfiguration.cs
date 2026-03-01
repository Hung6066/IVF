using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class SecretRotationScheduleConfiguration : IEntityTypeConfiguration<SecretRotationSchedule>
{
    public void Configure(EntityTypeBuilder<SecretRotationSchedule> builder)
    {
        builder.ToTable("secret_rotation_schedules");
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.SecretPath).IsUnique();
        builder.HasIndex(s => new { s.IsActive, s.NextRotationAt });

        builder.Property(s => s.SecretPath).HasMaxLength(500).IsRequired();
        builder.Property(s => s.RotationStrategy).HasMaxLength(50).HasDefaultValue("generate");
        builder.Property(s => s.CallbackUrl).HasMaxLength(2000);
    }
}
