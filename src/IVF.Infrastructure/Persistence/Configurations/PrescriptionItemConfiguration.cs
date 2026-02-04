using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.ToTable("prescription_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.DrugCode).HasMaxLength(30);
        builder.Property(i => i.DrugName).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Dosage).HasMaxLength(100);
        builder.Property(i => i.Frequency).HasMaxLength(100);
        builder.Property(i => i.Duration).HasMaxLength(100);

        builder.HasOne(i => i.Prescription).WithMany(p => p.Items).HasForeignKey(i => i.PrescriptionId);

        builder.HasIndex(i => i.PrescriptionId);
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
