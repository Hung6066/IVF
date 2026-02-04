using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

// ==================== SEMEN ANALYSIS ====================
public class SemenAnalysisConfiguration : IEntityTypeConfiguration<SemenAnalysis>
{
    public void Configure(EntityTypeBuilder<SemenAnalysis> builder)
    {
        builder.ToTable("semen_analyses");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.AnalysisType).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.Volume).HasPrecision(10, 2);
        builder.Property(s => s.Ph).HasPrecision(4, 2);
        builder.Property(s => s.Concentration).HasPrecision(10, 2);
        builder.Property(s => s.TotalCount).HasPrecision(10, 2);
        builder.Property(s => s.ProgressiveMotility).HasPrecision(5, 2);
        builder.Property(s => s.NonProgressiveMotility).HasPrecision(5, 2);
        builder.Property(s => s.Immotile).HasPrecision(5, 2);
        builder.Property(s => s.NormalMorphology).HasPrecision(5, 2);
        builder.Property(s => s.Vitality).HasPrecision(5, 2);
        builder.Property(s => s.PostWashConcentration).HasPrecision(10, 2);
        builder.Property(s => s.PostWashMotility).HasPrecision(5, 2);

        builder.HasOne(s => s.Patient).WithMany().HasForeignKey(s => s.PatientId);
        builder.HasOne(s => s.Cycle).WithMany().HasForeignKey(s => s.CycleId);

        builder.HasIndex(s => s.PatientId);
        builder.HasIndex(s => s.CycleId);
        builder.HasIndex(s => s.AnalysisDate);
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

// ==================== SPERM DONOR ====================
public class SpermDonorConfiguration : IEntityTypeConfiguration<SpermDonor>
{
    public void Configure(EntityTypeBuilder<SpermDonor> builder)
    {
        builder.ToTable("sperm_donors");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DonorCode).IsRequired().HasMaxLength(20);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.BloodType).HasMaxLength(10);
        builder.Property(d => d.Height).HasPrecision(5, 2);
        builder.Property(d => d.Weight).HasPrecision(5, 2);
        builder.Property(d => d.EyeColor).HasMaxLength(30);
        builder.Property(d => d.HairColor).HasMaxLength(30);
        builder.Property(d => d.Ethnicity).HasMaxLength(50);
        builder.Property(d => d.Education).HasMaxLength(100);
        builder.Property(d => d.Occupation).HasMaxLength(100);

        builder.HasOne(d => d.Patient).WithMany().HasForeignKey(d => d.PatientId);

        builder.HasIndex(d => d.DonorCode).IsUnique();
        builder.HasIndex(d => d.Status);
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}

// ==================== SPERM SAMPLE ====================
public class SpermSampleConfiguration : IEntityTypeConfiguration<SpermSample>
{
    public void Configure(EntityTypeBuilder<SpermSample> builder)
    {
        builder.ToTable("sperm_samples");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SampleCode).IsRequired().HasMaxLength(30);
        builder.Property(s => s.SpecimenType).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Volume).HasPrecision(10, 2);
        builder.Property(s => s.Concentration).HasPrecision(10, 2);
        builder.Property(s => s.Motility).HasPrecision(5, 2);

        builder.HasOne(s => s.Donor).WithMany(d => d.SpermSamples).HasForeignKey(s => s.DonorId);
        builder.HasOne(s => s.CryoLocation).WithMany().HasForeignKey(s => s.CryoLocationId);

        builder.HasIndex(s => s.SampleCode).IsUnique();
        builder.HasIndex(s => s.DonorId);
        builder.HasIndex(s => s.IsAvailable);
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

// ==================== INVOICE ====================
public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(30);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.SubTotal).HasPrecision(18, 2);
        builder.Property(i => i.DiscountPercent).HasPrecision(5, 2);
        builder.Property(i => i.DiscountAmount).HasPrecision(18, 2);
        builder.Property(i => i.TaxPercent).HasPrecision(5, 2);
        builder.Property(i => i.TaxAmount).HasPrecision(18, 2);
        builder.Property(i => i.TotalAmount).HasPrecision(18, 2);
        builder.Property(i => i.PaidAmount).HasPrecision(18, 2);

        builder.HasOne(i => i.Patient).WithMany().HasForeignKey(i => i.PatientId);
        builder.HasOne(i => i.Cycle).WithMany().HasForeignKey(i => i.CycleId);

        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.HasIndex(i => i.PatientId);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.InvoiceDate);
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}

// ==================== INVOICE ITEM ====================
public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("invoice_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ServiceCode).IsRequired().HasMaxLength(30);
        builder.Property(i => i.Description).IsRequired().HasMaxLength(500);
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        builder.Ignore(i => i.Amount); // Computed property

        builder.HasOne(i => i.Invoice).WithMany(inv => inv.Items).HasForeignKey(i => i.InvoiceId);

        builder.HasIndex(i => i.InvoiceId);
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}

// ==================== PAYMENT ====================
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PaymentNumber).IsRequired().HasMaxLength(30);
        builder.Property(p => p.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.TransactionReference).HasMaxLength(100);

        builder.HasOne(p => p.Invoice).WithMany(i => i.Payments).HasForeignKey(p => p.InvoiceId);

        builder.HasIndex(p => p.PaymentNumber).IsUnique();
        builder.HasIndex(p => p.InvoiceId);
        builder.HasIndex(p => p.PaymentDate);
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

// ==================== PRESCRIPTION ====================
public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("prescriptions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status).HasMaxLength(20);
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.HasOne(p => p.Patient).WithMany().HasForeignKey(p => p.PatientId);
        builder.HasOne(p => p.Cycle).WithMany().HasForeignKey(p => p.CycleId);
        builder.HasOne(p => p.Doctor).WithMany().HasForeignKey(p => p.DoctorId);

        builder.HasIndex(p => p.PatientId);
        builder.HasIndex(p => p.PrescriptionDate);
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

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

