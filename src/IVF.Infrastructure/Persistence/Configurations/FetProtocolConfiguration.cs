using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class FetProtocolConfiguration : IEntityTypeConfiguration<FetProtocol>
{
    public void Configure(EntityTypeBuilder<FetProtocol> builder)
    {
        builder.ToTable("fet_protocols");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.PrepType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(50).IsRequired();
        builder.Property(e => e.EstrogenDrug).HasMaxLength(200);
        builder.Property(e => e.EstrogenDose).HasMaxLength(100);
        builder.Property(e => e.ProgesteroneDrug).HasMaxLength(200);
        builder.Property(e => e.ProgesteroneDose).HasMaxLength(100);
        builder.Property(e => e.EndometriumPattern).HasMaxLength(100);
        builder.Property(e => e.EndometriumThickness).HasPrecision(5, 2);
        builder.Property(e => e.EmbryoGrade).HasMaxLength(20);
        builder.Property(e => e.Notes).HasMaxLength(2000);

        builder.HasIndex(e => e.CycleId).IsUnique();
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.Status);

        builder.HasOne(e => e.Cycle)
            .WithMany()
            .HasForeignKey(e => e.CycleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
