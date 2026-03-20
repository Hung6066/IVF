using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PrescriptionTemplateConfiguration : IEntityTypeConfiguration<PrescriptionTemplate>
{
    public void Configure(EntityTypeBuilder<PrescriptionTemplate> builder)
    {
        builder.ToTable("prescription_templates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.CycleType).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Description).HasMaxLength(500);

        builder.HasOne(t => t.CreatedByDoctor).WithMany().HasForeignKey(t => t.CreatedByDoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(t => t.Items).WithOne(i => i.Template).HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => t.CreatedByDoctorId);
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}

public class PrescriptionTemplateItemConfiguration : IEntityTypeConfiguration<PrescriptionTemplateItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionTemplateItem> builder)
    {
        builder.ToTable("prescription_template_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.MedicationName).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Dosage).IsRequired().HasMaxLength(50);
        builder.Property(i => i.Unit).IsRequired().HasMaxLength(20);
        builder.Property(i => i.Route).IsRequired().HasMaxLength(20);
        builder.Property(i => i.Frequency).IsRequired().HasMaxLength(50);
        builder.Property(i => i.Instructions).HasMaxLength(500);

        builder.HasIndex(i => i.TemplateId);
    }
}
