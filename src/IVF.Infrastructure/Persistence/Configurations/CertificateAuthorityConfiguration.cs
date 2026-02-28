using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class CertificateAuthorityConfiguration : IEntityTypeConfiguration<CertificateAuthority>
{
    public void Configure(EntityTypeBuilder<CertificateAuthority> builder)
    {
        builder.ToTable("CertificateAuthorities");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.CommonName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Organization).HasMaxLength(200);
        builder.Property(e => e.OrganizationalUnit).HasMaxLength(200);
        builder.Property(e => e.Country).HasMaxLength(10);
        builder.Property(e => e.State).HasMaxLength(100);
        builder.Property(e => e.Locality).HasMaxLength(100);
        builder.Property(e => e.KeyAlgorithm).HasMaxLength(20);
        builder.Property(e => e.Fingerprint).HasMaxLength(128);

        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => e.Fingerprint).IsUnique();

        builder.HasOne(e => e.ParentCa)
            .WithMany()
            .HasForeignKey(e => e.ParentCaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.IssuedCertificates)
            .WithOne(e => e.IssuingCa)
            .HasForeignKey(e => e.IssuingCaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
