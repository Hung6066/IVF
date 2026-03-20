using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class EggDonorRecipientConfiguration : IEntityTypeConfiguration<EggDonorRecipient>
{
    public void Configure(EntityTypeBuilder<EggDonorRecipient> builder)
    {
        builder.ToTable("egg_donor_recipients");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Notes).HasMaxLength(1000);

        builder.HasOne(m => m.EggDonor).WithMany().HasForeignKey(m => m.EggDonorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.RecipientCouple).WithMany().HasForeignKey(m => m.RecipientCoupleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Cycle).WithMany().HasForeignKey(m => m.CycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.MatchedBy).WithMany().HasForeignKey(m => m.MatchedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.EggDonorId);
        builder.HasIndex(m => m.RecipientCoupleId);
        builder.HasIndex(m => m.TenantId);
        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
