using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class FileTrackingConfiguration : IEntityTypeConfiguration<FileTracking>
{
    public void Configure(EntityTypeBuilder<FileTracking> builder)
    {
        builder.ToTable("file_trackings");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FileCode).IsRequired().HasMaxLength(50);
        builder.Property(f => f.CurrentLocation).IsRequired().HasMaxLength(200);
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Notes).HasMaxLength(1000);

        builder.HasOne(f => f.Patient).WithMany().HasForeignKey(f => f.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(f => f.Transfers).WithOne(t => t.File).HasForeignKey(t => t.FileTrackingId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => new { f.FileCode, f.TenantId }).IsUnique();
        builder.HasIndex(f => f.PatientId);
        builder.HasIndex(f => f.TenantId);
        builder.HasQueryFilter(f => !f.IsDeleted);
    }
}

public class FileTransferConfiguration : IEntityTypeConfiguration<FileTransfer>
{
    public void Configure(EntityTypeBuilder<FileTransfer> builder)
    {
        builder.ToTable("file_transfers");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.FromLocation).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ToLocation).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Reason).HasMaxLength(500);

        builder.HasOne(t => t.TransferredBy).WithMany().HasForeignKey(t => t.TransferredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => t.FileTrackingId);
    }
}
