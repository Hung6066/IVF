using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class AdverseEventDataConfiguration : IEntityTypeConfiguration<AdverseEventData>
{
    public void Configure(EntityTypeBuilder<AdverseEventData> builder)
    {
        builder.ToTable("adverse_event_data");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.EventType).HasMaxLength(100);
        builder.Property(t => t.Severity).HasMaxLength(20);
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.Treatment).HasMaxLength(2000);
        builder.Property(t => t.Outcome).HasMaxLength(500);

        builder.HasOne(t => t.Cycle)
            .WithMany(c => c.AdverseEvents)
            .HasForeignKey(t => t.CycleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
