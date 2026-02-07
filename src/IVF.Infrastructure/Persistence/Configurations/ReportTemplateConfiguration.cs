using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class ReportTemplateConfiguration : IEntityTypeConfiguration<ReportTemplate>
{
    public void Configure(EntityTypeBuilder<ReportTemplate> builder)
    {
        builder.ToTable("report_templates");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasMaxLength(1000);

        builder.Property(r => r.ConfigurationJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(r => r.ReportType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasOne(r => r.FormTemplate)
            .WithMany(t => t.ReportTemplates)
            .HasForeignKey(r => r.FormTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.CreatedByUser)
            .WithMany()
            .HasForeignKey(r => r.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
