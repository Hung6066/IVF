using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class Patient : BaseEntity
{
    public string PatientCode { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public DateTime DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public string? IdentityNumber { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public PatientType PatientType { get; private set; }
    public PatientStatus Status { get; private set; } = PatientStatus.Active;

    // Enterprise fields — demographics & compliance
    public string? Ethnicity { get; private set; }
    public string? Nationality { get; private set; }
    public string? Occupation { get; private set; }
    public string? InsuranceNumber { get; private set; }
    public string? InsuranceProvider { get; private set; }
    public BloodType? BloodType { get; private set; }
    public string? Allergies { get; private set; }
    public string? EmergencyContactName { get; private set; }
    public string? EmergencyContactPhone { get; private set; }
    public string? EmergencyContactRelation { get; private set; }
    public string? ReferralSource { get; private set; }
    public Guid? ReferringDoctorId { get; private set; }
    public string? MedicalNotes { get; private set; }

    // Consent & compliance tracking
    public bool ConsentDataProcessing { get; private set; }
    public DateTime? ConsentDataProcessingDate { get; private set; }
    public bool ConsentResearch { get; private set; }
    public DateTime? ConsentResearchDate { get; private set; }
    public bool ConsentMarketing { get; private set; }
    public DateTime? ConsentMarketingDate { get; private set; }
    public DateTime? DataRetentionExpiryDate { get; private set; }
    public bool IsAnonymized { get; private set; }
    public DateTime? AnonymizedAt { get; private set; }

    // GDPR Art. 18 — Right to restriction of processing
    public bool IsRestricted { get; private set; }
    public DateTime? RestrictedAt { get; private set; }
    public string? RestrictionReason { get; private set; }

    // Risk & priority
    public RiskLevel RiskLevel { get; private set; } = RiskLevel.Low;
    public string? RiskNotes { get; private set; }
    public PatientPriority Priority { get; private set; } = PatientPriority.Normal;

    // Activity tracking
    public DateTime? LastVisitDate { get; private set; }
    public int TotalVisits { get; private set; }
    public string? Tags { get; private set; }
    public string? Notes { get; private set; }

    // Navigation properties - Biometrics (1:1 for photo, 1:N for fingerprints)
    public virtual PatientPhoto? Photo { get; private set; }
    public virtual ICollection<PatientFingerprint> Fingerprints { get; private set; } = new List<PatientFingerprint>();

    // Navigation properties - Couples
    public virtual ICollection<Couple> AsWife { get; private set; } = new List<Couple>();
    public virtual ICollection<Couple> AsHusband { get; private set; } = new List<Couple>();
    public virtual ICollection<QueueTicket> QueueTickets { get; private set; } = new List<QueueTicket>();

    // Computed properties
    public bool HasPhoto => Photo != null;
    public bool HasFingerprint => Fingerprints.Any();
    public int Age => CalculateAge();

    private Patient() { }

    public static Patient Create(
        string patientCode,
        string fullName,
        DateTime dateOfBirth,
        Gender gender,
        PatientType patientType,
        string? identityNumber = null,
        string? phone = null,
        string? email = null,
        string? address = null,
        string? ethnicity = null,
        string? nationality = null,
        string? occupation = null,
        string? insuranceNumber = null,
        string? insuranceProvider = null,
        BloodType? bloodType = null,
        string? allergies = null,
        string? emergencyContactName = null,
        string? emergencyContactPhone = null,
        string? emergencyContactRelation = null,
        string? referralSource = null,
        Guid? referringDoctorId = null)
    {
        return new Patient
        {
            PatientCode = patientCode,
            FullName = fullName,
            DateOfBirth = dateOfBirth,
            Gender = gender,
            PatientType = patientType,
            IdentityNumber = identityNumber,
            Phone = phone,
            Email = email,
            Address = address,
            Ethnicity = ethnicity,
            Nationality = nationality,
            Occupation = occupation,
            InsuranceNumber = insuranceNumber,
            InsuranceProvider = insuranceProvider,
            BloodType = bloodType,
            Allergies = allergies,
            EmergencyContactName = emergencyContactName,
            EmergencyContactPhone = emergencyContactPhone,
            EmergencyContactRelation = emergencyContactRelation,
            ReferralSource = referralSource,
            ReferringDoctorId = referringDoctorId,
            Status = PatientStatus.Active,
            TotalVisits = 0
        };
    }

    public void Update(string fullName, string? phone, string? address)
    {
        FullName = fullName;
        Phone = phone;
        Address = address;
        SetUpdated();
    }

    public void UpdateDemographics(
        string? email,
        string? ethnicity,
        string? nationality,
        string? occupation,
        string? insuranceNumber,
        string? insuranceProvider,
        BloodType? bloodType,
        string? allergies)
    {
        Email = email;
        Ethnicity = ethnicity;
        Nationality = nationality;
        Occupation = occupation;
        InsuranceNumber = insuranceNumber;
        InsuranceProvider = insuranceProvider;
        BloodType = bloodType;
        Allergies = allergies;
        SetUpdated();
    }

    public void UpdateEmergencyContact(string? name, string? phone, string? relation)
    {
        EmergencyContactName = name;
        EmergencyContactPhone = phone;
        EmergencyContactRelation = relation;
        SetUpdated();
    }

    public void UpdateConsent(bool dataProcessing, bool research, bool marketing)
    {
        var now = DateTime.UtcNow;
        if (dataProcessing != ConsentDataProcessing)
        {
            ConsentDataProcessing = dataProcessing;
            ConsentDataProcessingDate = now;
        }
        if (research != ConsentResearch)
        {
            ConsentResearch = research;
            ConsentResearchDate = now;
        }
        if (marketing != ConsentMarketing)
        {
            ConsentMarketing = marketing;
            ConsentMarketingDate = now;
        }
        SetUpdated();
    }

    public void SetRiskLevel(RiskLevel level, string? notes)
    {
        RiskLevel = level;
        RiskNotes = notes;
        SetUpdated();
    }

    public void SetPriority(PatientPriority priority)
    {
        Priority = priority;
        SetUpdated();
    }

    public void ChangeStatus(PatientStatus newStatus)
    {
        Status = newStatus;
        SetUpdated();
    }

    public void RecordVisit()
    {
        LastVisitDate = DateTime.UtcNow;
        TotalVisits++;
        SetUpdated();
    }

    public void SetMedicalNotes(string? notes)
    {
        MedicalNotes = notes;
        SetUpdated();
    }

    public void UpdateTags(string? tags)
    {
        Tags = tags;
        SetUpdated();
    }

    public void SetNotes(string? notes)
    {
        Notes = notes;
        SetUpdated();
    }

    public void Anonymize()
    {
        FullName = "ANONYMIZED";
        IdentityNumber = null;
        Phone = null;
        Email = null;
        Address = null;
        Ethnicity = null;
        Nationality = null;
        Occupation = null;
        InsuranceNumber = null;
        InsuranceProvider = null;
        EmergencyContactName = null;
        EmergencyContactPhone = null;
        EmergencyContactRelation = null;
        IsAnonymized = true;
        AnonymizedAt = DateTime.UtcNow;
        Status = PatientStatus.Anonymized;
        SetUpdated();
    }

    public void SetDataRetentionExpiry(DateTime expiryDate)
    {
        DataRetentionExpiryDate = expiryDate;
        SetUpdated();
    }

    public void RestrictProcessing(string reason)
    {
        IsRestricted = true;
        RestrictedAt = DateTime.UtcNow;
        RestrictionReason = reason;
        SetUpdated();
    }

    public void LiftRestriction()
    {
        IsRestricted = false;
        RestrictedAt = null;
        RestrictionReason = null;
        SetUpdated();
    }

    private int CalculateAge()
    {
        var today = DateTime.Today;
        var age = today.Year - DateOfBirth.Year;
        if (DateOfBirth.Date > today.AddYears(-age)) age--;
        return age;
    }
}

