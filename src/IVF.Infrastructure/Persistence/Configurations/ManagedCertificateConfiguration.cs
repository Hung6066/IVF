using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class ManagedCertificateConfiguration : IEntityTypeConfiguration<ManagedCertificate>
{
    public void Configure(EntityTypeBuilder<ManagedCertificate> builder)
    {
        builder.ToTable("ManagedCertificates");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CommonName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SubjectAltNames).HasMaxLength(1000);
        builder.Property(e => e.Purpose).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Fingerprint).HasMaxLength(128);
        builder.Property(e => e.SerialNumber).HasMaxLength(100);
        builder.Property(e => e.KeyAlgorithm).HasMaxLength(20);
        builder.Property(e => e.DeployedTo).HasMaxLength(200);
        builder.Property(e => e.LastRenewalResult).HasMaxLength(500);

        builder.HasIndex(e => e.Fingerprint).IsUnique();
        builder.HasIndex(e => e.Purpose);
        builder.HasIndex(e => e.Status);
    }
}
