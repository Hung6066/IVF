using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class CloudBackupConfigConfiguration : IEntityTypeConfiguration<CloudBackupConfig>
{
    public void Configure(EntityTypeBuilder<CloudBackupConfig> builder)
    {
        builder.ToTable("cloud_backup_config");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Provider)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.S3Region).HasMaxLength(50);
        builder.Property(c => c.S3BucketName).HasMaxLength(200);
        builder.Property(c => c.S3AccessKey).HasMaxLength(200);
        builder.Property(c => c.S3SecretKey).HasMaxLength(500);
        builder.Property(c => c.S3ServiceUrl).HasMaxLength(500);

        builder.Property(c => c.AzureConnectionString).HasMaxLength(1000);
        builder.Property(c => c.AzureContainerName).HasMaxLength(200);

        builder.Property(c => c.GcsProjectId).HasMaxLength(200);
        builder.Property(c => c.GcsBucketName).HasMaxLength(200);
        builder.Property(c => c.GcsCredentialsPath).HasMaxLength(500);
    }
}
