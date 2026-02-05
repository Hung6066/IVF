using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class TransferDataConfiguration : IEntityTypeConfiguration<TransferData>
{
    public void Configure(EntityTypeBuilder<TransferData> builder)
    {
        builder.ToTable("transfer_data");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.LabNote).HasMaxLength(2000);

        builder.HasOne(t => t.Cycle)
            .WithOne(c => c.Transfer)
            .HasForeignKey<TransferData>(t => t.CycleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
