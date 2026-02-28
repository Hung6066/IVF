using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class ZTPolicyConfiguration : IEntityTypeConfiguration<ZTPolicy>
{
    public void Configure(EntityTypeBuilder<ZTPolicy> builder)
    {
        builder.ToTable("zt_policies");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(p => p.Action).IsUnique();

        builder.Property(p => p.RequiredAuthLevel)
            .HasMaxLength(50);

        builder.Property(p => p.MaxAllowedRisk)
            .HasMaxLength(20);

        builder.Property(p => p.AllowedCountries)
            .HasMaxLength(500);

        builder.HasIndex(p => p.IsActive);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
