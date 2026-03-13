using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class WafEventConfiguration : IEntityTypeConfiguration<WafEvent>
{
    public void Configure(EntityTypeBuilder<WafEvent> builder)
    {
        builder.ToTable("waf_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.RuleName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.RuleGroup).IsRequired();
        builder.Property(e => e.Action).IsRequired();
        builder.Property(e => e.ClientIp).HasMaxLength(45).IsRequired(); // IPv6 max
        builder.Property(e => e.Country).HasMaxLength(10);
        builder.Property(e => e.RequestPath).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.RequestMethod).HasMaxLength(10).IsRequired();
        builder.Property(e => e.QueryString).HasMaxLength(4000);
        builder.Property(e => e.UserAgent).HasMaxLength(1000);
        builder.Property(e => e.MatchedPattern).HasMaxLength(500);
        builder.Property(e => e.MatchedValue).HasMaxLength(500);
        builder.Property(e => e.Headers).HasColumnType("jsonb");
        builder.Property(e => e.CorrelationId).HasMaxLength(64);
        builder.Property(e => e.ProcessingTimeMs).IsRequired();

        // Indexes for common queries
        builder.HasIndex(e => e.ClientIp);
        builder.HasIndex(e => e.WafRuleId);
        builder.HasIndex(e => e.Action);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => new { e.Action, e.CreatedAt });
        builder.HasIndex(e => new { e.ClientIp, e.CreatedAt });
    }
}
