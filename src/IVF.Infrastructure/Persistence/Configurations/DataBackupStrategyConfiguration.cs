using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class DataBackupStrategyConfiguration : IEntityTypeConfiguration<DataBackupStrategy>
{
    public void Configure(EntityTypeBuilder<DataBackupStrategy> builder)
    {
        builder.ToTable("data_backup_strategies");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(b => b.Description)
            .HasMaxLength(500);

        builder.Property(b => b.CronExpression)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.RetentionDays)
            .IsRequired();

        builder.Property(b => b.MaxBackupCount)
            .IsRequired();

        builder.Property(b => b.LastRunOperationCode)
            .HasMaxLength(100);

        builder.Property(b => b.LastRunStatus)
            .HasMaxLength(20);

        builder.HasIndex(b => b.Enabled);

        builder.HasQueryFilter(b => !b.IsDeleted);
    }
}
