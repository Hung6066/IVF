using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Appointment entity for scheduling patient visits
/// </summary>
public class Appointment : BaseEntity
{
    public Guid PatientId { get; private set; }
    public Guid? CycleId { get; private set; }
    public Guid? DoctorId { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public int DurationMinutes { get; private set; } = 30;
    public AppointmentType Type { get; private set; }
    public AppointmentStatus Status { get; private set; } = AppointmentStatus.Scheduled;
    public string? Notes { get; private set; }
    public string? RoomNumber { get; private set; }
    
    // Navigation
    public virtual Patient Patient { get; private set; } = null!;
    public virtual TreatmentCycle? Cycle { get; private set; }
    public virtual Doctor? Doctor { get; private set; }
    
    private Appointment() { }
    
    public static Appointment Create(
        Guid patientId,
        DateTime scheduledAt,
        AppointmentType type,
        Guid? cycleId = null,
        Guid? doctorId = null,
        int durationMinutes = 30,
        string? notes = null,
        string? roomNumber = null)
    {
        return new Appointment
        {
            PatientId = patientId,
            CycleId = cycleId,
            DoctorId = doctorId,
            ScheduledAt = scheduledAt,
            DurationMinutes = durationMinutes,
            Type = type,
            Notes = notes,
            RoomNumber = roomNumber
        };
    }
    
    public void Confirm()
    {
        Status = AppointmentStatus.Confirmed;
        SetUpdated();
    }
    
    public void CheckIn()
    {
        Status = AppointmentStatus.CheckedIn;
        SetUpdated();
    }
    
    public void Complete()
    {
        Status = AppointmentStatus.Completed;
        SetUpdated();
    }
    
    public void Cancel(string? reason = null)
    {
        Status = AppointmentStatus.Cancelled;
        Notes = string.IsNullOrEmpty(Notes) ? reason : $"{Notes}\nCancelled: {reason}";
        SetUpdated();
    }
    
    public void NoShow()
    {
        Status = AppointmentStatus.NoShow;
        SetUpdated();
    }
    
    public void Reschedule(DateTime newDateTime)
    {
        ScheduledAt = newDateTime;
        Status = AppointmentStatus.Rescheduled;
        SetUpdated();
    }
    
    public void AssignDoctor(Guid doctorId)
    {
        DoctorId = doctorId;
        SetUpdated();
    }
    
    public void AssignRoom(string roomNumber)
    {
        RoomNumber = roomNumber;
        SetUpdated();
    }
}
