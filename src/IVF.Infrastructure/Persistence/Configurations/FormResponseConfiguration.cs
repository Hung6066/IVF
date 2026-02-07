using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class FormResponseConfiguration : IEntityTypeConfiguration<FormResponse>
{
    public void Configure(EntityTypeBuilder<FormResponse> builder)
    {
        builder.ToTable("form_responses");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.Notes)
            .HasMaxLength(2000);

        builder.HasOne(r => r.FormTemplate)
            .WithMany(t => t.Responses)
            .HasForeignKey(r => r.FormTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Patient)
            .WithMany()
            .HasForeignKey(r => r.PatientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.Cycle)
            .WithMany()
            .HasForeignKey(r => r.CycleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.SubmittedByUser)
            .WithMany()
            .HasForeignKey(r => r.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.FieldValues)
            .WithOne(v => v.FormResponse)
            .HasForeignKey(v => v.FormResponseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.FormTemplateId);
        builder.HasIndex(r => r.PatientId);
        builder.HasIndex(r => r.SubmittedAt);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
