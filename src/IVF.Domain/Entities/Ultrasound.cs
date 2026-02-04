using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class Ultrasound : BaseEntity
{
    public Guid CycleId { get; private set; }
    public DateTime ExamDate { get; private set; }
    public string UltrasoundType { get; private set; } = string.Empty; // PhụKhoa, NangNoãn, NMTC, Thai
    public int? LeftOvaryCount { get; private set; }
    public int? RightOvaryCount { get; private set; }
    public decimal? EndometriumThickness { get; private set; }
    public string? LeftFollicles { get; private set; }  // JSON array
    public string? RightFollicles { get; private set; } // JSON array
    public string? Findings { get; private set; }
    public Guid? DoctorId { get; private set; }

    // Navigation properties
    public virtual TreatmentCycle Cycle { get; private set; } = null!;
    public virtual User? Doctor { get; private set; }

    private Ultrasound() { }

    public static Ultrasound Create(
        Guid cycleId,
        DateTime examDate,
        string ultrasoundType,
        Guid? doctorId = null)
    {
        return new Ultrasound
        {
            CycleId = cycleId,
            ExamDate = examDate,
            UltrasoundType = ultrasoundType,
            DoctorId = doctorId
        };
    }

    public void RecordFollicles(
        int? leftCount,
        int? rightCount,
        string? leftFollicles,
        string? rightFollicles,
        decimal? endometriumThickness,
        string? findings)
    {
        LeftOvaryCount = leftCount;
        RightOvaryCount = rightCount;
        LeftFollicles = leftFollicles;
        RightFollicles = rightFollicles;
        EndometriumThickness = endometriumThickness;
        Findings = findings;
        SetUpdated();
    }
}
