using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class CertDeploymentLogConfiguration : IEntityTypeConfiguration<CertDeploymentLog>
{
    public void Configure(EntityTypeBuilder<CertDeploymentLog> builder)
    {
        builder.ToTable("CertDeploymentLogs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.OperationId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Target).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Container).HasMaxLength(100).IsRequired();
        builder.Property(e => e.RemoteHost).HasMaxLength(255);
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);

        // Store LogLines as JSON column (PostgreSQL jsonb)
        builder.OwnsMany(e => e.LogLines, nav =>
        {
            nav.ToJson();
        });

        builder.HasIndex(e => e.OperationId).IsUnique();
        builder.HasIndex(e => e.CertificateId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.StartedAt);

        builder.HasOne(e => e.Certificate)
            .WithMany()
            .HasForeignKey(e => e.CertificateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
