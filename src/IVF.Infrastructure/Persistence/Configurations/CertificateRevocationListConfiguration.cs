using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class CertificateRevocationListConfiguration : IEntityTypeConfiguration<CertificateRevocationList>
{
    public void Configure(EntityTypeBuilder<CertificateRevocationList> builder)
    {
        builder.ToTable("CertificateRevocationLists");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CrlNumber).IsRequired();
        builder.Property(e => e.Fingerprint).HasMaxLength(128).IsRequired();

        builder.HasIndex(e => new { e.CaId, e.CrlNumber }).IsUnique();
        builder.HasIndex(e => e.NextUpdate);

        builder.HasOne(e => e.Ca)
            .WithMany()
            .HasForeignKey(e => e.CaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
