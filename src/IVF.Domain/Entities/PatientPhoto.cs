using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class PatientPhoto : BaseEntity
{
    public Guid PatientId { get; private set; }
    public byte[] PhotoData { get; private set; } = Array.Empty<byte>();
    public string ContentType { get; private set; } = "image/jpeg";
    public string? FileName { get; private set; }
    public DateTime UploadedAt { get; private set; } = DateTime.UtcNow;

    // Navigation
    public virtual Patient Patient { get; private set; } = null!;

    private PatientPhoto() { }

    public static PatientPhoto Create(
        Guid patientId,
        byte[] photoData,
        string contentType,
        string? fileName = null)
    {
        return new PatientPhoto
        {
            PatientId = patientId,
            PhotoData = photoData,
            ContentType = contentType,
            FileName = fileName,
            UploadedAt = DateTime.UtcNow
        };
    }

    public void UpdatePhoto(byte[] photoData, string contentType, string? fileName)
    {
        PhotoData = photoData;
        ContentType = contentType;
        FileName = fileName;
        UploadedAt = DateTime.UtcNow;
        SetUpdated();
    }
}
