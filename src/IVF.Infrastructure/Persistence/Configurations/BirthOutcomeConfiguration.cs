using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class BirthOutcomeConfiguration : IEntityTypeConfiguration<BirthOutcome>
{
    public void Configure(EntityTypeBuilder<BirthOutcome> builder)
    {
        builder.ToTable("birth_outcomes");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Gender).HasMaxLength(20).IsRequired();
        builder.Property(b => b.Weight).HasPrecision(7, 2);

        builder.HasOne(b => b.BirthData)
            .WithMany(d => d.Outcomes)
            .HasForeignKey(b => b.BirthDataId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.BirthDataId);
        builder.HasQueryFilter(b => !b.IsDeleted);
    }
}
