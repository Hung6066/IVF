using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class BackupOperationConfiguration : IEntityTypeConfiguration<BackupOperation>
{
    public void Configure(EntityTypeBuilder<BackupOperation> builder)
    {
        builder.ToTable("backup_operations");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.OperationCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(b => b.OperationCode).IsUnique();

        builder.Property(b => b.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.ArchivePath)
            .HasMaxLength(500);

        builder.Property(b => b.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(b => b.StartedBy)
            .HasMaxLength(100);

        builder.Property(b => b.LogLinesJson)
            .HasColumnType("jsonb");

        builder.Property(b => b.CloudStorageKey)
            .HasMaxLength(500);

        builder.HasIndex(b => b.Status);
        builder.HasIndex(b => b.StartedAt);
        builder.HasIndex(b => b.Type);

        // No soft-delete filter — keep all operation history
    }
}
