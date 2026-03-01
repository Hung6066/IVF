using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.ToTable("security_events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Severity)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Username)
            .HasMaxLength(100);

        builder.Property(e => e.IpAddress)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(e => e.UserAgent)
            .HasMaxLength(500);

        builder.Property(e => e.DeviceFingerprint)
            .HasMaxLength(128);

        builder.Property(e => e.Country)
            .HasMaxLength(10);

        builder.Property(e => e.City)
            .HasMaxLength(100);

        builder.Property(e => e.RequestPath)
            .HasMaxLength(500);

        builder.Property(e => e.RequestMethod)
            .HasMaxLength(10);

        builder.Property(e => e.Details)
            .HasColumnType("jsonb");

        builder.Property(e => e.ThreatIndicators)
            .HasColumnType("jsonb");

        builder.Property(e => e.RiskScore)
            .HasPrecision(5, 2);

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(100);

        builder.Property(e => e.SessionId)
            .HasMaxLength(100);

        // Indexes for query performance
        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.IpAddress);
        builder.HasIndex(e => e.Severity);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => new { e.EventType, e.CreatedAt });
        builder.HasIndex(e => new { e.UserId, e.CreatedAt });
        builder.HasIndex(e => new { e.Severity, e.CreatedAt });
        builder.HasIndex(e => e.CorrelationId);
    }
}
