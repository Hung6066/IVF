using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class ConsentForm : BaseEntity, ITenantEntity
{
    public Guid PatientId { get; private set; }
    public Guid? CycleId { get; private set; }
    public Guid? ProcedureId { get; private set; }
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }

    public string ConsentType { get; private set; } = string.Empty; // OPU, IUI, Anesthesia, EggDonation, SpermDonation, FET, General
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? TemplateContent { get; private set; } // Nội dung mẫu consent

    // Signing
    public string Status { get; private set; } = "Pending"; // Pending, Signed, Revoked, Expired
    public DateTime? SignedAt { get; private set; }
    public Guid? SignedByPatientId { get; private set; }
    public string? PatientSignature { get; private set; } // Base64 signature image
    public Guid? WitnessUserId { get; private set; }
    public string? WitnessSignature { get; private set; }
    public Guid? DoctorUserId { get; private set; }
    public string? DoctorSignature { get; private set; }

    // Document
    public string? ScannedDocumentUrl { get; private set; } // URL to uploaded scan
    public DateTime? ExpiresAt { get; private set; }
    public string? RevokeReason { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? Notes { get; private set; }

    // Navigation
    public virtual Patient Patient { get; private set; } = null!;
    public virtual TreatmentCycle? Cycle { get; private set; }
    public virtual Procedure? Procedure { get; private set; }

    private ConsentForm() { }

    public static ConsentForm Create(
        Guid patientId,
        string consentType,
        string title,
        string? description = null,
        string? templateContent = null,
        Guid? cycleId = null,
        Guid? procedureId = null,
        DateTime? expiresAt = null,
        string? notes = null)
    {
        return new ConsentForm
        {
            PatientId = patientId,
            ConsentType = consentType,
            Title = title,
            Description = description,
            TemplateContent = templateContent,
            CycleId = cycleId,
            ProcedureId = procedureId,
            ExpiresAt = expiresAt,
            Notes = notes
        };
    }

    public void Sign(Guid patientId, string? patientSignature, Guid? witnessUserId, string? witnessSignature, Guid? doctorUserId, string? doctorSignature)
    {
        if (Status != "Pending")
            throw new InvalidOperationException($"Cannot sign consent with status '{Status}'");

        SignedByPatientId = patientId;
        PatientSignature = patientSignature;
        WitnessUserId = witnessUserId;
        WitnessSignature = witnessSignature;
        DoctorUserId = doctorUserId;
        DoctorSignature = doctorSignature;
        SignedAt = DateTime.UtcNow;
        Status = "Signed";
        SetUpdated();
    }

    public void Revoke(string reason)
    {
        if (Status != "Signed")
            throw new InvalidOperationException("Only signed consents can be revoked");

        RevokeReason = reason;
        RevokedAt = DateTime.UtcNow;
        Status = "Revoked";
        SetUpdated();
    }

    public void UploadScan(string documentUrl)
    {
        ScannedDocumentUrl = documentUrl;
        SetUpdated();
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        SetUpdated();
    }

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    public bool IsValid => Status == "Signed" && !IsExpired;
}
