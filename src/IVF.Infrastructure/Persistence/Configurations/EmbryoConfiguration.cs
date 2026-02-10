using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class EmbryoConfiguration : IEntityTypeConfiguration<Embryo>
{
    public void Configure(EntityTypeBuilder<Embryo> builder)
    {
        builder.ToTable("embryos");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Grade)
            .HasConversion<string>()
            .HasMaxLength(5);

        builder.Property(e => e.Day)
            .HasConversion<string>()
            .HasMaxLength(5);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(e => e.Cycle)
            .WithMany(c => c.Embryos)
            .HasForeignKey(e => e.CycleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CryoLocation)
            .WithMany()
            .HasForeignKey(e => e.CryoLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CycleId);
        builder.HasIndex(e => e.CryoLocationId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
