namespace IVF.Domain.Enums;

public enum Gender
{
    Male,
    Female
}

public enum PatientType
{
    Infertility,
    EggDonor,
    SpermDonor
}

public enum QueueType
{
    Reception,      // Tiếp đón
    Consultation,   // Tư vấn
    Ultrasound,     // Siêu âm
    LabTest,        // Xét nghiệm
    Andrology,      // Nam khoa
    Pharmacy        // Nhà thuốc
}

public enum TicketStatus
{
    Waiting,
    Called,
    InService,
    Completed,
    Skipped,
    Cancelled
}

public enum TreatmentMethod
{
    QHTN,   // Quan hệ tự nhiên
    IUI,    // Intrauterine Insemination
    ICSI,   // Intracytoplasmic Sperm Injection
    IVM     // In Vitro Maturation
}

public enum CyclePhase
{
    Consultation,
    OvarianStimulation,     // KTBT - Kích thích buồng trứng
    TriggerShot,            // KTRT - Kích thích rụng trứng
    EggRetrieval,           // Chọc hút
    EmbryoCulture,          // Nuôi phôi
    EmbryoTransfer,         // Chuyển phôi
    LutealSupport,          // Hỗ trợ hoàng thể
    PregnancyTest,          // Thử thai
    Completed
}

public enum CycleOutcome
{
    Ongoing,
    Pregnant,
    NotPregnant,
    Cancelled,
    FrozenAll       // Trữ toàn bộ
}

public enum EmbryoGrade
{
    AA, AB, BA, BB,
    AC, CA, BC, CB,
    CC, CD, DC, DD
}

public enum EmbryoDay
{
    D1, D2, D3, D4, D5, D6
}

public enum EmbryoStatus
{
    Developing,
    Transferred,
    Frozen,
    Thawed,
    Discarded,
    Arrested
}

public enum SpecimenType
{
    Embryo,
    Sperm,
    Oocyte
}

public enum DonorStatus
{
    Screening,
    Active,
    Suspended,
    Retired,
    Inactive,
    Rejected
}

public enum InvoiceStatus
{
    Draft,
    Issued,
    PartiallyPaid,
    Paid,
    Refunded,
    Cancelled
}

public enum PaymentMethod
{
    Cash,
    Card,
    Transfer,
    Insurance
}

public enum AnalysisType
{
    PreWash,
    PostWash
}
