using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class BackupScheduleConfigConfiguration : IEntityTypeConfiguration<BackupScheduleConfig>
{
    public void Configure(EntityTypeBuilder<BackupScheduleConfig> builder)
    {
        builder.ToTable("backup_schedule_config");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.CronExpression)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.LastScheduledOperationCode)
            .HasMaxLength(100);

        builder.Property(b => b.RetentionDays)
            .IsRequired();

        builder.Property(b => b.MaxBackupCount)
            .IsRequired();
    }
}
