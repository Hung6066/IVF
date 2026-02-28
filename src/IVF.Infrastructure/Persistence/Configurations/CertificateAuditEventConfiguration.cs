using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class CertificateAuditEventConfiguration : IEntityTypeConfiguration<CertificateAuditEvent>
{
    public void Configure(EntityTypeBuilder<CertificateAuditEvent> builder)
    {
        builder.ToTable("CertificateAuditEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Description).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.Actor).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SourceIp).HasMaxLength(50);
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.CertificateId);
        builder.HasIndex(e => e.CaId);
        builder.HasIndex(e => e.CreatedAt);
    }
}
