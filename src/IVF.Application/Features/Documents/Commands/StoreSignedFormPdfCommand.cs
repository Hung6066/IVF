using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Documents.Commands;

/// <summary>
/// Lưu PDF đã ký số vào MinIO gắn với bệnh nhân
/// Được gọi sau khi export-pdf?sign=true hoàn tất signing
/// Path: {patientCode}/Forms/{templateCode}/{year}/{formResponseId}.pdf
/// </summary>
public sealed record StoreSignedFormPdfCommand(
    Guid FormResponseId,
    Guid PatientId,
    string PatientCode,
    string TemplateName,
    byte[] SignedPdfBytes,
    string? SignerNames,
    string? FormTemplateCode = null, // Code của form template, dùng để build path trong MinIO
    Guid? UploadedByUserId = null,
    Guid? CycleId = null
) : IRequest<Result<PatientDocumentDto>>;

/// <summary>
/// Lấy PDF ký số đã lưu trong MinIO theo FormResponseId
/// </summary>
public sealed record GetStoredSignedPdfQuery(
    Guid FormResponseId
) : IRequest<StoredSignedPdfResult?>;

/// <summary>
/// Vô hiệu hóa PDF ký số đã lưu (khi có thay đổi chữ ký)
/// </summary>
public sealed record InvalidateStoredSignedPdfCommand(
    Guid FormResponseId
) : IRequest<bool>;

public sealed record StoredSignedPdfResult(
    Guid DocumentId,
    string BucketName,
    string ObjectKey,
    string OriginalFileName,
    long FileSizeBytes,
    DateTime? SignedAt,
    string? SignerNames,
    string? DownloadUrl);
