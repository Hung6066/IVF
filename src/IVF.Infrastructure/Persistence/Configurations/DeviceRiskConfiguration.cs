using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class DeviceRiskConfiguration : IEntityTypeConfiguration<DeviceRisk>
{
    public void Configure(EntityTypeBuilder<DeviceRisk> builder)
    {
        builder.ToTable("device_risks");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.UserId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.DeviceId)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.RiskLevel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.RiskScore)
            .HasPrecision(5, 2);

        builder.Property(d => d.Factors)
            .HasMaxLength(2000);

        builder.Property(d => d.IpAddress)
            .HasMaxLength(50);

        builder.Property(d => d.Country)
            .HasMaxLength(10);

        builder.Property(d => d.UserAgent)
            .HasMaxLength(1000);

        builder.HasIndex(d => new { d.UserId, d.DeviceId });
        builder.HasIndex(d => d.RiskLevel);

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
