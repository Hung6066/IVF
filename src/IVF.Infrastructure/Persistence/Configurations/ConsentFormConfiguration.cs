using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class ConsentFormConfiguration : IEntityTypeConfiguration<ConsentForm>
{
    public void Configure(EntityTypeBuilder<ConsentForm> builder)
    {
        builder.ToTable("consent_forms");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ConsentType).IsRequired().HasMaxLength(30);
        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.TemplateContent).HasMaxLength(10000);
        builder.Property(c => c.Status).IsRequired().HasMaxLength(20);
        builder.Property(c => c.ScannedDocumentUrl).HasMaxLength(500);
        builder.Property(c => c.RevokeReason).HasMaxLength(500);
        builder.Property(c => c.Notes).HasMaxLength(2000);

        builder.HasOne(c => c.Patient).WithMany().HasForeignKey(c => c.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Cycle).WithMany().HasForeignKey(c => c.CycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Procedure).WithMany().HasForeignKey(c => c.ProcedureId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.PatientId);
        builder.HasIndex(c => c.CycleId);
        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => c.ConsentType);
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
