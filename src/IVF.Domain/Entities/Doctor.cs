using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Doctor entity - extends User with medical-specific properties
/// </summary>
public class Doctor : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Specialty { get; private set; } = string.Empty;  // IVF, Andrology, Gynecology
    public string? LicenseNumber { get; private set; }
    public string? RoomNumber { get; private set; }
    public string? Schedule { get; private set; }  // JSON schedule data
    public bool IsAvailable { get; private set; } = true;
    public int MaxPatientsPerDay { get; private set; } = 20;
    
    // Navigation
    public virtual User User { get; private set; } = null!;
    
    private Doctor() { }
    
    public static Doctor Create(
        Guid userId,
        string specialty,
        string? licenseNumber = null,
        string? roomNumber = null,
        int maxPatientsPerDay = 20)
    {
        return new Doctor
        {
            UserId = userId,
            Specialty = specialty,
            LicenseNumber = licenseNumber,
            RoomNumber = roomNumber,
            MaxPatientsPerDay = maxPatientsPerDay
        };
    }
    
    public void UpdateSchedule(string schedule)
    {
        Schedule = schedule;
        SetUpdated();
    }
    
    public void UpdateRoom(string roomNumber)
    {
        RoomNumber = roomNumber;
        SetUpdated();
    }
    
    public void SetAvailable(bool available)
    {
        IsAvailable = available;
        SetUpdated();
    }
    
    public void UpdateMaxPatients(int max)
    {
        MaxPatientsPerDay = max;
        SetUpdated();
    }
}
