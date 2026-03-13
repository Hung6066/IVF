using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class WafRuleConfiguration : IEntityTypeConfiguration<WafRule>
{
    public void Configure(EntityTypeBuilder<WafRule> builder)
    {
        builder.ToTable("waf_rules");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.Priority).IsRequired();
        builder.Property(e => e.IsEnabled).IsRequired();
        builder.Property(e => e.RuleGroup).IsRequired();
        builder.Property(e => e.IsManaged).IsRequired();

        // JSONB columns for pattern arrays
        builder.Property(e => e.UriPathPatterns).HasColumnType("jsonb");
        builder.Property(e => e.QueryStringPatterns).HasColumnType("jsonb");
        builder.Property(e => e.HeaderPatterns).HasColumnType("jsonb");
        builder.Property(e => e.BodyPatterns).HasColumnType("jsonb");
        builder.Property(e => e.Methods).HasColumnType("jsonb");
        builder.Property(e => e.IpCidrList).HasColumnType("jsonb");
        builder.Property(e => e.CountryCodes).HasColumnType("jsonb");
        builder.Property(e => e.UserAgentPatterns).HasColumnType("jsonb");

        builder.Property(e => e.MatchType).IsRequired();
        builder.Property(e => e.NegateMatch).IsRequired();
        builder.Property(e => e.Expression).HasMaxLength(2000);

        builder.Property(e => e.Action).IsRequired();
        builder.Property(e => e.BlockResponseMessage).HasMaxLength(500);

        builder.Property(e => e.HitCount).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(200);
        builder.Property(e => e.LastModifiedBy).HasMaxLength(200);

        // Composite index for rule evaluation query
        builder.HasIndex(e => new { e.IsEnabled, e.Priority });

        // Soft-delete filter
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
