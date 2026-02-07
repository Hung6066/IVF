using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class FormTemplateConfiguration : IEntityTypeConfiguration<FormTemplate>
{
    public void Configure(EntityTypeBuilder<FormTemplate> builder)
    {
        builder.ToTable("form_templates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.Property(t => t.Version)
            .HasMaxLength(20)
            .HasDefaultValue("1.0");

        builder.HasOne(t => t.Category)
            .WithMany(c => c.FormTemplates)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CreatedByUser)
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Fields)
            .WithOne(f => f.FormTemplate)
            .HasForeignKey(f => f.FormTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Responses)
            .WithOne(r => r.FormTemplate)
            .HasForeignKey(r => r.FormTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.ReportTemplates)
            .WithOne(r => r.FormTemplate)
            .HasForeignKey(r => r.FormTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
