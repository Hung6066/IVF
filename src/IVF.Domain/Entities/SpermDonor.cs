using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Sperm donor for donor sperm bank
/// </summary>
public class SpermDonor : BaseEntity
{
    public string DonorCode { get; private set; } = string.Empty;
    public Guid PatientId { get; private set; } // Link to Patient for medical records
    public DonorStatus Status { get; private set; }
    
    // Physical characteristics
    public string? BloodType { get; private set; }
    public decimal? Height { get; private set; } // cm
    public decimal? Weight { get; private set; } // kg
    public string? EyeColor { get; private set; }
    public string? HairColor { get; private set; }
    public string? Ethnicity { get; private set; }
    public string? Education { get; private set; }
    public string? Occupation { get; private set; }
    
    // Screening
    public DateTime? ScreeningDate { get; private set; }
    public DateTime? LastDonationDate { get; private set; }
    public int TotalDonations { get; private set; }
    public int SuccessfulPregnancies { get; private set; }
    
    public string? MedicalHistory { get; private set; }
    public string? Notes { get; private set; }
    
    // Navigation
    public Patient Patient { get; private set; } = null!;
    public ICollection<SpermSample> SpermSamples { get; private set; } = new List<SpermSample>();

    private SpermDonor() { }

    public static SpermDonor Create(string donorCode, Guid patientId)
    {
        return new SpermDonor
        {
            Id = Guid.NewGuid(),
            DonorCode = donorCode,
            PatientId = patientId,
            Status = DonorStatus.Active,
            TotalDonations = 0,
            SuccessfulPregnancies = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(
        string? bloodType, decimal? height, decimal? weight,
        string? eyeColor, string? hairColor, string? ethnicity,
        string? education, string? occupation)
    {
        BloodType = bloodType;
        Height = height;
        Weight = weight;
        EyeColor = eyeColor;
        HairColor = hairColor;
        Ethnicity = ethnicity;
        Education = education;
        Occupation = occupation;
        SetUpdated();
    }

    public void RecordScreening(DateTime screeningDate, string? medicalHistory)
    {
        ScreeningDate = screeningDate;
        MedicalHistory = medicalHistory;
        SetUpdated();
    }

    public void RecordDonation()
    {
        TotalDonations++;
        LastDonationDate = DateTime.UtcNow;
        SetUpdated();
    }

    public void RecordPregnancy()
    {
        SuccessfulPregnancies++;
        SetUpdated();
    }

    public void Suspend()
    {
        Status = DonorStatus.Suspended;
        SetUpdated();
    }

    public void Retire()
    {
        Status = DonorStatus.Retired;
        SetUpdated();
    }

    public void Reactivate()
    {
        Status = DonorStatus.Active;
        SetUpdated();
    }
}

/// <summary>
/// Stored sperm sample from a donor
/// </summary>
public class SpermSample : BaseEntity
{
    public Guid DonorId { get; private set; }
    public string SampleCode { get; private set; } = string.Empty;
    public DateTime CollectionDate { get; private set; }
    public SpecimenType SpecimenType { get; private set; }
    
    // Quality
    public decimal? Volume { get; private set; }
    public decimal? Concentration { get; private set; }
    public decimal? Motility { get; private set; }
    public int? VialCount { get; private set; }
    
    // Storage
    public Guid? CryoLocationId { get; private set; }
    public DateTime? FreezeDate { get; private set; }
    public DateTime? ThawDate { get; private set; }
    public bool IsAvailable { get; private set; }
    
    // Navigation
    public SpermDonor Donor { get; private set; } = null!;
    public CryoLocation? CryoLocation { get; private set; }

    private SpermSample() { }

    public static SpermSample Create(
        Guid donorId,
        string sampleCode,
        DateTime collectionDate,
        SpecimenType specimenType)
    {
        return new SpermSample
        {
            Id = Guid.NewGuid(),
            DonorId = donorId,
            SampleCode = sampleCode,
            CollectionDate = collectionDate,
            SpecimenType = specimenType,
            IsAvailable = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void RecordQuality(decimal? volume, decimal? concentration, decimal? motility, int? vialCount)
    {
        Volume = volume;
        Concentration = concentration;
        Motility = motility;
        VialCount = vialCount;
        SetUpdated();
    }

    public void Freeze(Guid cryoLocationId)
    {
        CryoLocationId = cryoLocationId;
        FreezeDate = DateTime.UtcNow;
        SetUpdated();
    }

    public void Reserve()
    {
        IsAvailable = false;
        SetUpdated();
    }

    public void Use()
    {
        IsAvailable = false;
        ThawDate = DateTime.UtcNow;
        SetUpdated();
    }
}
