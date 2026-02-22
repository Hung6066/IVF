using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Tài liệu bệnh án điện tử - lưu trữ trên MinIO S3
/// Cấu trúc lưu trữ: {bucket}/{patientCode}/{documentType}/{year}/{filename}
/// Tham khảo chuẩn HL7 FHIR DocumentReference
/// </summary>
public class PatientDocument : BaseEntity
{
    // ─── Liên kết bệnh nhân ───
    public Guid PatientId { get; private set; }
    public virtual Patient Patient { get; private set; } = null!;

    // ─── Liên kết tùy chọn ───
    public Guid? CycleId { get; private set; }           // Chu kỳ điều trị liên quan
    public Guid? FormResponseId { get; private set; }     // Phiếu khám liên quan
    public Guid? UploadedByUserId { get; private set; }   // Người tải lên

    // ─── Thông tin tài liệu ───
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Active;
    public ConfidentialityLevel Confidentiality { get; private set; } = ConfidentialityLevel.Normal;

    // ─── Thông tin file trên MinIO ───
    public string BucketName { get; private set; } = string.Empty;
    public string ObjectKey { get; private set; } = string.Empty;      // Full path in S3: BN001/LabResult/2026/abc.pdf
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string? Checksum { get; private set; }                      // SHA-256 hash for integrity

    // ─── Ký số ───
    public bool IsSigned { get; private set; }
    public string? SignedByName { get; private set; }
    public DateTime? SignedAt { get; private set; }
    public string? SignedObjectKey { get; private set; }               // Key của bản PDF đã ký

    // ─── Phiên bản ───
    public int Version { get; private set; } = 1;
    public Guid? PreviousVersionId { get; private set; }
    public virtual PatientDocument? PreviousVersion { get; private set; }

    // ─── Tags & metadata ───
    public string? Tags { get; private set; }                          // JSON array of tags
    public string? MetadataJson { get; private set; }                  // Extra metadata as JSON

    private PatientDocument() { }

    public static PatientDocument Create(
        Guid patientId,
        string title,
        DocumentType documentType,
        string bucketName,
        string objectKey,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        string? checksum = null,
        string? description = null,
        Guid? cycleId = null,
        Guid? formResponseId = null,
        Guid? uploadedByUserId = null,
        ConfidentialityLevel confidentiality = ConfidentialityLevel.Normal,
        string? tags = null,
        string? metadataJson = null)
    {
        return new PatientDocument
        {
            PatientId = patientId,
            Title = title,
            DocumentType = documentType,
            BucketName = bucketName,
            ObjectKey = objectKey,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            Checksum = checksum,
            Description = description,
            CycleId = cycleId,
            FormResponseId = formResponseId,
            UploadedByUserId = uploadedByUserId,
            Confidentiality = confidentiality,
            Tags = tags,
            MetadataJson = metadataJson
        };
    }

    public void MarkAsSigned(string signedByName, string signedObjectKey)
    {
        IsSigned = true;
        SignedByName = signedByName;
        SignedAt = DateTime.UtcNow;
        SignedObjectKey = signedObjectKey;
        SetUpdated();
    }

    public void UpdateStatus(DocumentStatus status)
    {
        Status = status;
        SetUpdated();
    }

    public void UpdateMetadata(string? title = null, string? description = null, string? tags = null)
    {
        if (title != null) Title = title;
        if (description != null) Description = description;
        if (tags != null) Tags = tags;
        SetUpdated();
    }

    public PatientDocument CreateNewVersion(
        string bucketName,
        string objectKey,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        string? checksum = null)
    {
        Status = DocumentStatus.Superseded;
        SetUpdated();

        return new PatientDocument
        {
            PatientId = PatientId,
            Title = Title,
            Description = Description,
            DocumentType = DocumentType,
            Confidentiality = Confidentiality,
            BucketName = bucketName,
            ObjectKey = objectKey,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            Checksum = checksum,
            CycleId = CycleId,
            FormResponseId = FormResponseId,
            UploadedByUserId = UploadedByUserId,
            Version = Version + 1,
            PreviousVersionId = Id,
            Tags = Tags,
            MetadataJson = MetadataJson
        };
    }

    /// <summary>
    /// Tạo object key theo cấu trúc chuẩn bệnh viện lớn:
    /// {patientCode}/{documentType}/{year}/{guid}{extension}
    /// </summary>
    public static string BuildObjectKey(
        string patientCode,
        DocumentType documentType,
        string originalFileName)
    {
        var ext = Path.GetExtension(originalFileName);
        var year = DateTime.UtcNow.Year;
        var uniqueId = Guid.NewGuid().ToString("N")[..12];
        return $"{patientCode}/{documentType}/{year}/{uniqueId}{ext}";
    }

    /// <summary>
    /// Tạo object key cho PDF ký số từ phiếu khám, có đủ ngữ nghĩa để tìm kiếm:
    /// {patientCode}/Forms/{templateCode}/{year}/{formResponseId}.pdf
    /// - Tìm tất cả phiếu của BN: prefix = BN-TEST-011/Forms/
    /// - Tìm theo loại phiếu:     prefix = BN-TEST-011/Forms/spermanalysis/
    /// - Trace về DB:             filename = {formResponseId}.pdf → query by FormResponseId
    /// </summary>
    public static string BuildSignedFormObjectKey(
        string patientCode,
        Guid formResponseId,
        string? templateCode = null)
    {
        var year = DateTime.UtcNow.Year;
        var slugCode = string.IsNullOrWhiteSpace(templateCode) ? "direct" : templateCode;
        return $"{patientCode}/Forms/{slugCode}/{year}/{formResponseId:N}.pdf";
    }
}
