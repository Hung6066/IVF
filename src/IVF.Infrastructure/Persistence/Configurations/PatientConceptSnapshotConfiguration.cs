using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class PatientConceptSnapshotConfiguration : IEntityTypeConfiguration<PatientConceptSnapshot>
{
    public void Configure(EntityTypeBuilder<PatientConceptSnapshot> builder)
    {
        builder.ToTable("patient_concept_snapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TextValue).HasMaxLength(4000);
        builder.Property(s => s.NumericValue).HasPrecision(18, 6);
        builder.Property(s => s.JsonValue).HasColumnType("text");

        builder.HasOne(s => s.Patient)
            .WithMany()
            .HasForeignKey(s => s.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Concept)
            .WithMany()
            .HasForeignKey(s => s.ConceptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.FormResponse)
            .WithMany()
            .HasForeignKey(s => s.FormResponseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.FormField)
            .WithMany()
            .HasForeignKey(s => s.FormFieldId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Cycle)
            .WithMany()
            .HasForeignKey(s => s.CycleId)
            .OnDelete(DeleteBehavior.SetNull);

        // Unique: one latest value per (patient, concept, cycle)
        // Use COALESCE to handle null CycleId
        builder.HasIndex(s => new { s.PatientId, s.ConceptId, s.CycleId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        // Fast lookup: all known data for a patient
        builder.HasIndex(s => s.PatientId)
            .HasFilter("\"IsDeleted\" = false");

        // Fast lookup: concept timeline across patients
        builder.HasIndex(s => new { s.ConceptId, s.PatientId })
            .HasFilter("\"IsDeleted\" = false");

        // FK lookup indexes
        builder.HasIndex(s => s.FormResponseId);
        builder.HasIndex(s => s.FormFieldId);
        builder.HasIndex(s => s.CycleId);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
