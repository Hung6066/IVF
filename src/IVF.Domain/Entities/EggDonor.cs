using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class EggDonor : BaseEntity
{
    public string DonorCode { get; private set; } = string.Empty;
    public Guid PatientId { get; private set; }
    public DonorStatus Status { get; private set; }

    // Physical characteristics
    public string? BloodType { get; private set; }
    public decimal? Height { get; private set; }
    public decimal? Weight { get; private set; }
    public string? EyeColor { get; private set; }
    public string? HairColor { get; private set; }
    public string? Ethnicity { get; private set; }
    public string? Education { get; private set; }
    public string? Occupation { get; private set; }

    // Screening & history
    public DateTime? ScreeningDate { get; private set; }
    public DateTime? LastDonationDate { get; private set; }
    public int TotalDonations { get; private set; }
    public int SuccessfulPregnancies { get; private set; }

    // Egg-specific
    public int? AmhLevel { get; private set; }
    public int? AntralFollicleCount { get; private set; }
    public string? MenstrualHistory { get; private set; }

    public string? MedicalHistory { get; private set; }
    public string? Notes { get; private set; }

    // Navigation
    public Patient Patient { get; private set; } = null!;
    public ICollection<OocyteSample> OocyteSamples { get; private set; } = new List<OocyteSample>();

    private EggDonor() { }

    public static EggDonor Create(string donorCode, Guid patientId) => new EggDonor
    {
        Id = Guid.NewGuid(),
        DonorCode = donorCode,
        PatientId = patientId,
        Status = DonorStatus.Active,
        TotalDonations = 0,
        SuccessfulPregnancies = 0,
        CreatedAt = DateTime.UtcNow
    };

    public void UpdateProfile(string? bloodType, decimal? height, decimal? weight,
        string? eyeColor, string? hairColor, string? ethnicity, string? education, string? occupation,
        int? amhLevel, int? antralFollicleCount, string? menstrualHistory)
    {
        BloodType = bloodType;
        Height = height;
        Weight = weight;
        EyeColor = eyeColor;
        HairColor = hairColor;
        Ethnicity = ethnicity;
        Education = education;
        Occupation = occupation;
        AmhLevel = amhLevel;
        AntralFollicleCount = antralFollicleCount;
        MenstrualHistory = menstrualHistory;
        SetUpdated();
    }

    public void RecordScreening(DateTime screeningDate, string? medicalHistory, string? notes)
    {
        ScreeningDate = screeningDate;
        MedicalHistory = medicalHistory;
        Notes = notes;
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

public class OocyteSample : BaseEntity
{
    public Guid DonorId { get; private set; }
    public string SampleCode { get; private set; } = string.Empty;
    public DateTime CollectionDate { get; private set; }

    // Oocyte data
    public int? TotalOocytes { get; private set; }
    public int? MatureOocytes { get; private set; } // MII
    public int? ImmatureOocytes { get; private set; } // MI + GV
    public int? DegeneratedOocytes { get; private set; }

    // Cryopreservation
    public int? VitrifiedCount { get; private set; }
    public Guid? CryoLocationId { get; private set; }
    public DateTime? FreezeDate { get; private set; }
    public DateTime? ThawDate { get; private set; }
    public int? SurvivedAfterThaw { get; private set; }
    public bool IsAvailable { get; private set; }

    public string? Notes { get; private set; }

    // Navigation
    public EggDonor Donor { get; private set; } = null!;
    public CryoLocation? CryoLocation { get; private set; }

    private OocyteSample() { }

    public static OocyteSample Create(Guid donorId, string sampleCode, DateTime collectionDate) => new OocyteSample
    {
        Id = Guid.NewGuid(),
        DonorId = donorId,
        SampleCode = sampleCode,
        CollectionDate = collectionDate,
        IsAvailable = true,
        CreatedAt = DateTime.UtcNow
    };

    public void RecordQuality(int? totalOocytes, int? matureOocytes, int? immatureOocytes, int? degeneratedOocytes, string? notes)
    {
        TotalOocytes = totalOocytes;
        MatureOocytes = matureOocytes;
        ImmatureOocytes = immatureOocytes;
        DegeneratedOocytes = degeneratedOocytes;
        Notes = notes;
        SetUpdated();
    }

    public void Vitrify(int count, DateTime freezeDate, Guid? cryoLocationId)
    {
        VitrifiedCount = count;
        FreezeDate = freezeDate;
        CryoLocationId = cryoLocationId;
        SetUpdated();
    }

    public void Thaw(DateTime thawDate, int survived)
    {
        ThawDate = thawDate;
        SurvivedAfterThaw = survived;
        IsAvailable = false;
        SetUpdated();
    }

    public void Reserve()
    {
        IsAvailable = false;
        SetUpdated();
    }

    public void Release()
    {
        IsAvailable = true;
        SetUpdated();
    }
}
