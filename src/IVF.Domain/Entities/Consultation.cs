using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class Consultation : BaseEntity, ITenantEntity
{
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid? CycleId { get; private set; }
    public Guid TenantId { get; private set; }
    public void SetTenantId(Guid tenantId) { TenantId = tenantId; SetUpdated(); }

    public string ConsultationType { get; private set; } = "FirstVisit"; // FirstVisit, FollowUp, TreatmentDecision
    public DateTime ConsultationDate { get; private set; }
    public string Status { get; private set; } = "Scheduled"; // Scheduled, InProgress, Completed, Cancelled

    // Clinical data
    public string? ChiefComplaint { get; private set; } // Lý do khám
    public string? MedicalHistory { get; private set; } // Bệnh sử
    public string? PastHistory { get; private set; } // Tiền căn
    public string? SurgicalHistory { get; private set; } // Tiền căn phẫu thuật
    public string? FamilyHistory { get; private set; } // Tiền sử gia đình
    public string? ObstetricHistory { get; private set; } // Tiền sản khoa
    public string? MenstrualHistory { get; private set; } // Kinh nguyệt
    public string? PhysicalExamination { get; private set; } // Khám lâm sàng
    public string? Diagnosis { get; private set; } // Chẩn đoán
    public string? TreatmentPlan { get; private set; } // Hướng điều trị
    public TreatmentMethod? RecommendedMethod { get; private set; } // Phương pháp đề xuất
    public string? Notes { get; private set; }
    public bool WaiveConsultationFee { get; private set; }

    // Navigation
    public virtual Patient Patient { get; private set; } = null!;
    public virtual User Doctor { get; private set; } = null!;
    public virtual TreatmentCycle? Cycle { get; private set; }

    private Consultation() { }

    public static Consultation Create(
        Guid patientId,
        Guid doctorId,
        DateTime consultationDate,
        string consultationType = "FirstVisit",
        Guid? cycleId = null,
        string? chiefComplaint = null,
        string? notes = null,
        bool waiveConsultationFee = false)
    {
        return new Consultation
        {
            PatientId = patientId,
            DoctorId = doctorId,
            ConsultationDate = consultationDate,
            ConsultationType = consultationType,
            CycleId = cycleId,
            ChiefComplaint = chiefComplaint,
            Notes = notes,
            WaiveConsultationFee = waiveConsultationFee
        };
    }

    public void Start()
    {
        Status = "InProgress";
        SetUpdated();
    }

    public void RecordClinicalData(
        string? chiefComplaint,
        string? medicalHistory,
        string? pastHistory,
        string? surgicalHistory,
        string? familyHistory,
        string? obstetricHistory,
        string? menstrualHistory,
        string? physicalExamination)
    {
        ChiefComplaint = chiefComplaint;
        MedicalHistory = medicalHistory;
        PastHistory = pastHistory;
        SurgicalHistory = surgicalHistory;
        FamilyHistory = familyHistory;
        ObstetricHistory = obstetricHistory;
        MenstrualHistory = menstrualHistory;
        PhysicalExamination = physicalExamination;
        SetUpdated();
    }

    public void RecordDiagnosis(string? diagnosis, string? treatmentPlan, TreatmentMethod? recommendedMethod)
    {
        Diagnosis = diagnosis;
        TreatmentPlan = treatmentPlan;
        RecommendedMethod = recommendedMethod;
        SetUpdated();
    }

    public void Complete()
    {
        Status = "Completed";
        SetUpdated();
    }

    public void Cancel()
    {
        Status = "Cancelled";
        SetUpdated();
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        SetUpdated();
    }
}
