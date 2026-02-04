using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class CryoLocationConfiguration : IEntityTypeConfiguration<CryoLocation>
{
    public void Configure(EntityTypeBuilder<CryoLocation> builder)
    {
        builder.ToTable("cryo_locations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Tank)
            .HasMaxLength(20);

        builder.Property(c => c.Canister)
            .HasMaxLength(20);

        builder.Property(c => c.Cane)
            .HasMaxLength(20);

        builder.Property(c => c.Goblet)
            .HasMaxLength(20);

        builder.Property(c => c.Straw)
            .HasMaxLength(20);

        builder.Property(c => c.SpecimenType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(c => new { c.Tank, c.Canister, c.Cane, c.Goblet, c.Straw }).IsUnique();
    }
}
