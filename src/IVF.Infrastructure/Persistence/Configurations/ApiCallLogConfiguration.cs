using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class ApiCallLogConfiguration : IEntityTypeConfiguration<ApiCallLog>
{
    public void Configure(EntityTypeBuilder<ApiCallLog> builder)
    {
        builder.ToTable("api_call_logs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Method).HasMaxLength(10).IsRequired();
        builder.Property(a => a.Path).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Username).HasMaxLength(100);
        builder.Property(a => a.IpAddress).HasMaxLength(50);
        builder.Property(a => a.UserAgent).HasMaxLength(500);

        builder.HasIndex(a => new { a.TenantId, a.RequestedAt });
        builder.HasIndex(a => a.RequestedAt);
    }
}
