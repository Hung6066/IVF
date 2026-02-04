using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class CoupleConfiguration : IEntityTypeConfiguration<Couple>
{
    public void Configure(EntityTypeBuilder<Couple> builder)
    {
        builder.ToTable("couples");

        builder.HasKey(c => c.Id);

        builder.HasOne(c => c.Wife)
            .WithMany(p => p.AsWife)
            .HasForeignKey(c => c.WifeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Husband)
            .WithMany(p => p.AsHusband)
            .HasForeignKey(c => c.HusbandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.SpermDonor)
            .WithMany()
            .HasForeignKey(c => c.SpermDonorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
