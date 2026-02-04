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
    public string? Address { get; private set; }
    public byte[]? Photo { get; private set; }
    public byte[]? Fingerprint { get; private set; }
    public PatientType PatientType { get; private set; }

    // Navigation properties
    public virtual ICollection<Couple> AsWife { get; private set; } = new List<Couple>();
    public virtual ICollection<Couple> AsHusband { get; private set; } = new List<Couple>();
    public virtual ICollection<QueueTicket> QueueTickets { get; private set; } = new List<QueueTicket>();

    private Patient() { }

    public static Patient Create(
        string patientCode,
        string fullName,
        DateTime dateOfBirth,
        Gender gender,
        PatientType patientType,
        string? identityNumber = null,
        string? phone = null,
        string? address = null)
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
            Address = address
        };
    }

    public void Update(string fullName, string? phone, string? address)
    {
        FullName = fullName;
        Phone = phone;
        Address = address;
        SetUpdated();
    }

    public void SetPhoto(byte[] photo)
    {
        Photo = photo;
        SetUpdated();
    }

    public void SetFingerprint(byte[] fingerprint)
    {
        Fingerprint = fingerprint;
        SetUpdated();
    }
}
